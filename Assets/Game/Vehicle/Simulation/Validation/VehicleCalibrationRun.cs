using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    [Serializable]
    public sealed class VehicleMetricTrialObservation
    {
        [SerializeField] private string metricId = string.Empty;
        [SerializeField] private VehicleStatisticalSummary summary;

        public string MetricId => metricId;
        public VehicleStatisticalSummary Summary => summary;

        public VehicleMetricTrialObservation()
        {
        }

        public VehicleMetricTrialObservation(string valueMetricId, VehicleStatisticalSummary valueSummary)
        {
            metricId = valueMetricId ?? string.Empty;
            summary = valueSummary;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(metricId))
            {
                failure = "Trial observation metric ID is empty.";
                return false;
            }

            if (!summary.Validate(out failure))
            {
                failure = $"Trial observation '{metricId}' has an invalid summary: {failure}";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationTrial
    {
        [SerializeField, Min(0)] private int trialIndex;
        [SerializeField] private int seed;
        [SerializeField] private VehicleCalibrationTrialStatus status;
        [SerializeField, Min(0f)] private float elapsedSeconds;
        [SerializeField] private VehicleMetricTrialObservation[] observations = Array.Empty<VehicleMetricTrialObservation>();
        [SerializeField] private string notes = string.Empty;

        public int TrialIndex => trialIndex;
        public int Seed => seed;
        public VehicleCalibrationTrialStatus Status => status;
        public float ElapsedSeconds => elapsedSeconds;
        public VehicleMetricTrialObservation[] Observations => observations;
        public string Notes => notes;

        public VehicleCalibrationTrial()
        {
        }

        public VehicleCalibrationTrial(
            int valueTrialIndex,
            int valueSeed,
            VehicleCalibrationTrialStatus valueStatus,
            float valueElapsedSeconds,
            VehicleMetricTrialObservation[] valueObservations,
            string valueNotes)
        {
            trialIndex = valueTrialIndex;
            seed = valueSeed;
            status = valueStatus;
            elapsedSeconds = valueElapsedSeconds;
            observations = valueObservations ?? Array.Empty<VehicleMetricTrialObservation>();
            notes = valueNotes ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (trialIndex < 0 || !VehicleCalibrationValidation.IsFiniteNonNegative(elapsedSeconds))
            {
                failure = "Trial index or elapsed time is invalid.";
                return false;
            }

            if (observations == null)
            {
                failure = "Trial observations are null.";
                return false;
            }

            var metricIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < observations.Length; index++)
            {
                VehicleMetricTrialObservation observation = observations[index];
                if (observation == null)
                {
                    failure = $"Trial observation at index {index} is null.";
                    return false;
                }

                if (!observation.Validate(out failure))
                {
                    return false;
                }

                if (!metricIds.Add(observation.MetricId))
                {
                    failure = $"Trial contains duplicate observation '{observation.MetricId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationRun
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string runId = string.Empty;
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string startedUtc = string.Empty;
        [SerializeField] private string completedUtc = string.Empty;
        [SerializeField] private VehicleCalibrationRunStatus status;
        [SerializeField] private VehicleCalibrationEnvironment environment = new VehicleCalibrationEnvironment();
        [SerializeField] private VehicleCalibrationTrial[] trials = Array.Empty<VehicleCalibrationTrial>();
        [SerializeField] private VehicleMetricResult[] metricResults = Array.Empty<VehicleMetricResult>();
        [SerializeField] private string failure = string.Empty;

        public int SchemaVersion => schemaVersion;
        public string RunId => runId;
        public string ProfileId => profileId;
        public string FixtureId => fixtureId;
        public string StartedUtc => startedUtc;
        public string CompletedUtc => completedUtc;
        public VehicleCalibrationRunStatus Status => status;
        public VehicleCalibrationEnvironment Environment => environment;
        public VehicleCalibrationTrial[] Trials => trials;
        public VehicleMetricResult[] MetricResults => metricResults;
        public string Failure => failure;

        public VehicleCalibrationRun()
        {
        }

        public VehicleCalibrationRun(
            string valueRunId,
            string valueProfileId,
            string valueFixtureId,
            string valueStartedUtc,
            VehicleCalibrationRunStatus valueStatus,
            VehicleCalibrationEnvironment valueEnvironment)
        {
            Configure(
                valueRunId,
                valueProfileId,
                valueFixtureId,
                valueStartedUtc,
                string.Empty,
                valueStatus,
                valueEnvironment,
                Array.Empty<VehicleCalibrationTrial>(),
                Array.Empty<VehicleMetricResult>(),
                string.Empty);
        }

        public void Configure(
            string valueRunId,
            string valueProfileId,
            string valueFixtureId,
            string valueStartedUtc,
            string valueCompletedUtc,
            VehicleCalibrationRunStatus valueStatus,
            VehicleCalibrationEnvironment valueEnvironment,
            VehicleCalibrationTrial[] valueTrials,
            VehicleMetricResult[] valueMetricResults,
            string valueFailure)
        {
            schemaVersion = CurrentSchemaVersion;
            runId = valueRunId ?? string.Empty;
            profileId = valueProfileId ?? string.Empty;
            fixtureId = valueFixtureId ?? string.Empty;
            startedUtc = valueStartedUtc ?? string.Empty;
            completedUtc = valueCompletedUtc ?? string.Empty;
            status = valueStatus;
            environment = valueEnvironment;
            trials = valueTrials ?? Array.Empty<VehicleCalibrationTrial>();
            metricResults = valueMetricResults ?? Array.Empty<VehicleMetricResult>();
            failure = valueFailure ?? string.Empty;
        }

        public void Complete(
            string valueCompletedUtc,
            VehicleCalibrationTrial[] valueTrials,
            VehicleMetricResult[] valueMetricResults,
            string valueFailure = "")
        {
            completedUtc = valueCompletedUtc ?? string.Empty;
            trials = valueTrials ?? Array.Empty<VehicleCalibrationTrial>();
            metricResults = valueMetricResults ?? Array.Empty<VehicleMetricResult>();
            failure = valueFailure ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(failure))
            {
                status = VehicleCalibrationRunStatus.Failed;
                return;
            }

            status = ResolveStatus(trials, metricResults);
        }

        public bool Validate(out string validationFailure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                validationFailure = $"Unsupported calibration run schema version {schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(runId) ||
                string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(fixtureId) ||
                string.IsNullOrWhiteSpace(startedUtc))
            {
                validationFailure = "Run, profile, fixture and start-time fields must be explicit.";
                return false;
            }

            if (environment == null)
            {
                validationFailure = "Calibration run has no environment snapshot.";
                return false;
            }

            if (!environment.Validate(out validationFailure))
            {
                validationFailure = $"Calibration run environment is invalid: {validationFailure}";
                return false;
            }

            if (trials == null || metricResults == null)
            {
                validationFailure = "Calibration run trial or metric-result arrays are null.";
                return false;
            }

            var trialIndices = new HashSet<int>();
            for (int index = 0; index < trials.Length; index++)
            {
                VehicleCalibrationTrial trial = trials[index];
                if (trial == null)
                {
                    validationFailure = $"Calibration trial at index {index} is null.";
                    return false;
                }

                if (!trial.Validate(out validationFailure))
                {
                    validationFailure = $"Invalid calibration trial at index {index}: {validationFailure}";
                    return false;
                }

                if (!trialIndices.Add(trial.TrialIndex))
                {
                    validationFailure = $"Duplicate calibration trial index {trial.TrialIndex}.";
                    return false;
                }
            }

            var resultMetricIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < metricResults.Length; index++)
            {
                VehicleMetricResult result = metricResults[index];
                if (result == null)
                {
                    validationFailure = $"Metric result at index {index} is null.";
                    return false;
                }

                if (!result.Validate(out validationFailure))
                {
                    validationFailure = $"Invalid metric result at index {index}: {validationFailure}";
                    return false;
                }

                if (!string.Equals(result.RunId, runId, StringComparison.Ordinal) ||
                    !string.Equals(result.FixtureId, fixtureId, StringComparison.Ordinal))
                {
                    validationFailure = "Metric result run or fixture ID does not match its run.";
                    return false;
                }

                if (!resultMetricIds.Add(result.MetricId))
                {
                    validationFailure = $"Duplicate metric result '{result.MetricId}'.";
                    return false;
                }
            }

            if (status != VehicleCalibrationRunStatus.NotStarted &&
                status != VehicleCalibrationRunStatus.Running &&
                string.IsNullOrWhiteSpace(completedUtc))
            {
                validationFailure = "A terminal calibration run requires a completion time.";
                return false;
            }

            if (status != VehicleCalibrationRunStatus.NotStarted &&
                status != VehicleCalibrationRunStatus.Running)
            {
                VehicleCalibrationRunStatus expectedStatus =
                    !string.IsNullOrWhiteSpace(failure)
                        ? VehicleCalibrationRunStatus.Failed
                        : ResolveStatus(trials, metricResults);
                if (status != expectedStatus)
                {
                    validationFailure =
                        $"Calibration run status '{status}' does not match resolved status '{expectedStatus}'.";
                    return false;
                }
            }

            validationFailure = string.Empty;
            return true;
        }

        private static VehicleCalibrationRunStatus ResolveStatus(
            VehicleCalibrationTrial[] runTrials,
            VehicleMetricResult[] results)
        {
            bool hasInvalidConfiguration = false;
            bool hasFailure = false;
            bool hasBlocked = false;
            bool hasInconclusive = false;
            if (runTrials != null)
            {
                for (int index = 0; index < runTrials.Length; index++)
                {
                    VehicleCalibrationTrial trial = runTrials[index];
                    if (trial == null)
                    {
                        hasInvalidConfiguration = true;
                        continue;
                    }

                    switch (trial.Status)
                    {
                        case VehicleCalibrationTrialStatus.Failed:
                        case VehicleCalibrationTrialStatus.InvalidNumericState:
                            hasFailure = true;
                            break;
                        case VehicleCalibrationTrialStatus.Blocked:
                            hasBlocked = true;
                            break;
                        case VehicleCalibrationTrialStatus.NotStarted:
                            hasInconclusive = true;
                            break;
                    }
                }
            }

            if (results != null)
            {
                for (int index = 0; index < results.Length; index++)
                {
                    VehicleMetricResult result = results[index];
                    if (result == null)
                    {
                        hasInvalidConfiguration = true;
                        continue;
                    }

                    switch (result.Status)
                    {
                        case VehicleMetricResultStatus.Failed:
                        case VehicleMetricResultStatus.InvalidSamples:
                            hasFailure = true;
                            break;
                        case VehicleMetricResultStatus.InvalidConfiguration:
                            hasInvalidConfiguration = true;
                            break;
                        case VehicleMetricResultStatus.Blocked:
                            hasBlocked = true;
                            break;
                        case VehicleMetricResultStatus.Inconclusive:
                        case VehicleMetricResultStatus.UnknownReference:
                        case VehicleMetricResultStatus.NotEvaluated:
                            hasInconclusive = true;
                            break;
                    }
                }
            }

            if (hasInvalidConfiguration)
            {
                return VehicleCalibrationRunStatus.InvalidConfiguration;
            }

            if (hasFailure)
            {
                return VehicleCalibrationRunStatus.Failed;
            }

            if (hasBlocked)
            {
                return VehicleCalibrationRunStatus.Blocked;
            }

            if (results == null || results.Length == 0)
            {
                return VehicleCalibrationRunStatus.Inconclusive;
            }

            return hasInconclusive
                ? VehicleCalibrationRunStatus.Inconclusive
                : VehicleCalibrationRunStatus.Passed;
        }
    }
}
