using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    [Serializable]
    public sealed class VehicleCalibrationFixture
    {
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private VehicleCalibrationFixtureKind kind;
        [SerializeField] private VehicleCalibrationFixtureStatus status;
        [SerializeField, Min(0f)] private float warmupSeconds;
        [SerializeField, Min(0.001f)] private float durationSeconds = 1f;
        [SerializeField, Min(1)] private int trialCount = 1;
        [SerializeField, Min(0f)] private float repeatedRunTolerance01;
        [SerializeField] private VehicleCalibrationEnvironment environment = new VehicleCalibrationEnvironment();
        [SerializeField] private string[] metricIds = Array.Empty<string>();
        [SerializeField] private string blockedReason = string.Empty;
        [SerializeField, TextArea] private string notes = string.Empty;

        public string FixtureId => fixtureId;
        public string DisplayName => displayName;
        public VehicleCalibrationFixtureKind Kind => kind;
        public VehicleCalibrationFixtureStatus Status => status;
        public float WarmupSeconds => warmupSeconds;
        public float DurationSeconds => durationSeconds;
        public int TrialCount => trialCount;
        public float RepeatedRunTolerance01 => repeatedRunTolerance01;
        public VehicleCalibrationEnvironment Environment => environment;
        public string[] MetricIds => metricIds;
        public string BlockedReason => blockedReason;
        public string Notes => notes;
        public bool CanRun => status == VehicleCalibrationFixtureStatus.Ready;

        public VehicleCalibrationFixture()
        {
        }

        public VehicleCalibrationFixture(
            string valueFixtureId,
            string valueDisplayName,
            VehicleCalibrationFixtureKind valueKind,
            VehicleCalibrationFixtureStatus valueStatus,
            float valueWarmupSeconds,
            float valueDurationSeconds,
            int valueTrialCount,
            float valueRepeatedRunTolerance01,
            VehicleCalibrationEnvironment valueEnvironment,
            string[] valueMetricIds,
            string valueBlockedReason,
            string valueNotes)
        {
            fixtureId = valueFixtureId ?? string.Empty;
            displayName = valueDisplayName ?? string.Empty;
            kind = valueKind;
            status = valueStatus;
            warmupSeconds = valueWarmupSeconds;
            durationSeconds = valueDurationSeconds;
            trialCount = valueTrialCount;
            repeatedRunTolerance01 = valueRepeatedRunTolerance01;
            environment = valueEnvironment;
            metricIds = valueMetricIds ?? Array.Empty<string>();
            blockedReason = valueBlockedReason ?? string.Empty;
            notes = valueNotes ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(fixtureId) || string.IsNullOrWhiteSpace(displayName))
            {
                failure = "Fixture ID and display name must be explicit.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFiniteNonNegative(warmupSeconds) ||
                !VehicleCalibrationValidation.IsFinitePositive(durationSeconds) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(repeatedRunTolerance01) ||
                trialCount < 1)
            {
                failure = $"Fixture '{fixtureId}' contains invalid duration, trial or tolerance values.";
                return false;
            }

            if (status == VehicleCalibrationFixtureStatus.Blocked)
            {
                if (string.IsNullOrWhiteSpace(blockedReason))
                {
                    failure = $"Blocked fixture '{fixtureId}' requires a reason.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            if (status == VehicleCalibrationFixtureStatus.Disabled)
            {
                failure = string.Empty;
                return true;
            }

            if (environment == null)
            {
                failure = $"Ready fixture '{fixtureId}' has no environment.";
                return false;
            }

            if (!environment.Validate(out failure))
            {
                failure = $"Fixture '{fixtureId}' environment is invalid: {failure}";
                return false;
            }

            if (environment.Input.Source == VehicleCalibrationInputSource.Scripted)
            {
                VehicleScriptedInputFrame[] frames = environment.Input.ScriptedFrames;
                if (frames.Length > 0 && frames[frames.Length - 1].TimeSeconds > durationSeconds)
                {
                    failure = $"Fixture '{fixtureId}' has scripted input after its duration.";
                    return false;
                }
            }

            if (metricIds == null || metricIds.Length == 0)
            {
                failure = $"Ready fixture '{fixtureId}' requires at least one metric.";
                return false;
            }

            var uniqueMetricIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < metricIds.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(metricIds[index]) || !uniqueMetricIds.Add(metricIds[index]))
                {
                    failure = $"Fixture '{fixtureId}' contains an empty or duplicate metric ID.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
