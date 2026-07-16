using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    [CreateAssetMenu(menuName = "MSC/Vehicle Simulation/Calibration Profile")]
    public sealed class VehicleCalibrationProfile : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string revision = string.Empty;
        [SerializeField] private VehicleSimulationConfig vehicleConfiguration;
        [SerializeField] private VehicleCalibrationFixture[] fixtures = Array.Empty<VehicleCalibrationFixture>();
        [SerializeField] private VehicleMetricDefinition[] metricDefinitions = Array.Empty<VehicleMetricDefinition>();
        [SerializeField] private VehicleReferenceTarget[] referenceTargets = Array.Empty<VehicleReferenceTarget>();
        [SerializeField, TextArea] private string notes = string.Empty;

        public int SchemaVersion => schemaVersion;
        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string Revision => revision;
        public VehicleSimulationConfig VehicleConfiguration => vehicleConfiguration;
        public VehicleCalibrationFixture[] Fixtures => fixtures;
        public VehicleMetricDefinition[] MetricDefinitions => metricDefinitions;
        public VehicleReferenceTarget[] ReferenceTargets => referenceTargets;
        public string Notes => notes;

        public void Configure(
            VehicleSimulationConfig valueVehicleConfiguration,
            string valueProfileId,
            string valueDisplayName,
            string valueRevision,
            VehicleCalibrationFixture[] valueFixtures,
            VehicleMetricDefinition[] valueMetricDefinitions,
            VehicleReferenceTarget[] valueReferenceTargets,
            string valueNotes)
        {
            schemaVersion = CurrentSchemaVersion;
            profileId = valueProfileId ?? string.Empty;
            displayName = valueDisplayName ?? string.Empty;
            revision = valueRevision ?? string.Empty;
            vehicleConfiguration = valueVehicleConfiguration;
            fixtures = valueFixtures ?? Array.Empty<VehicleCalibrationFixture>();
            metricDefinitions = valueMetricDefinitions ?? Array.Empty<VehicleMetricDefinition>();
            referenceTargets = valueReferenceTargets ?? Array.Empty<VehicleReferenceTarget>();
            notes = valueNotes ?? string.Empty;
        }

        public void Configure(
            string valueProfileId,
            string valueDisplayName,
            string valueRevision,
            VehicleCalibrationFixture[] valueFixtures,
            VehicleMetricDefinition[] valueMetricDefinitions,
            VehicleReferenceTarget[] valueReferenceTargets,
            string valueNotes)
        {
            Configure(
                vehicleConfiguration,
                valueProfileId,
                valueDisplayName,
                valueRevision,
                valueFixtures,
                valueMetricDefinitions,
                valueReferenceTargets,
                valueNotes);
        }

        public bool TryGetFixture(string valueFixtureId, out VehicleCalibrationFixture fixture)
        {
            if (fixtures != null)
            {
                for (int index = 0; index < fixtures.Length; index++)
                {
                    VehicleCalibrationFixture candidate = fixtures[index];
                    if (candidate != null &&
                        string.Equals(candidate.FixtureId, valueFixtureId, StringComparison.Ordinal))
                    {
                        fixture = candidate;
                        return true;
                    }
                }
            }

            fixture = null;
            return false;
        }

        public bool TryGetMetricDefinition(string valueMetricId, out VehicleMetricDefinition definition)
        {
            if (metricDefinitions != null)
            {
                for (int index = 0; index < metricDefinitions.Length; index++)
                {
                    VehicleMetricDefinition candidate = metricDefinitions[index];
                    if (candidate != null &&
                        string.Equals(candidate.MetricId, valueMetricId, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public string ComputeContentFingerprint()
        {
            var builder = new StringBuilder(4096);
            builder.Append(schemaVersion).Append('|')
                .Append(profileId).Append('|')
                .Append(displayName).Append('|')
                .Append(revision).Append('|')
                .Append(vehicleConfiguration != null ? vehicleConfiguration.ConfigurationId : string.Empty)
                .Append('|')
                .Append(vehicleConfiguration != null ? vehicleConfiguration.TuningSchemaVersion : 0)
                .Append('|')
                .Append(notes);
            AppendSerialized(builder, fixtures);
            AppendSerialized(builder, metricDefinitions);
            AppendSerialized(builder, referenceTargets);
            return Hash128.Compute(builder.ToString()).ToString();
        }

        private static void AppendSerialized<T>(StringBuilder builder, T[] values)
            where T : class
        {
            builder.Append('|').Append(values?.Length ?? -1);
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Length; index++)
            {
                builder.Append('|');
                T value = values[index];
                builder.Append(value == null ? "null" : JsonUtility.ToJson(value));
            }
        }

        public bool TryGetReferenceTarget(
            string valueFixtureId,
            string valueMetricId,
            out VehicleReferenceTarget target)
        {
            if (referenceTargets != null)
            {
                for (int index = 0; index < referenceTargets.Length; index++)
                {
                    VehicleReferenceTarget candidate = referenceTargets[index];
                    if (candidate != null &&
                        string.Equals(candidate.FixtureId, valueFixtureId, StringComparison.Ordinal) &&
                        string.Equals(candidate.MetricId, valueMetricId, StringComparison.Ordinal))
                    {
                        target = candidate;
                        return true;
                    }
                }
            }

            target = null;
            return false;
        }

        public bool Validate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported calibration profile schema version {schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(revision))
            {
                failure = "Profile ID, display name and revision must be explicit.";
                return false;
            }

            if (vehicleConfiguration == null)
            {
                failure = "Calibration profile has no vehicle simulation configuration.";
                return false;
            }

            if (!vehicleConfiguration.Validate(out failure))
            {
                failure = $"Calibration profile vehicle configuration is invalid: {failure}";
                return false;
            }

            if (fixtures == null || fixtures.Length == 0 ||
                metricDefinitions == null || metricDefinitions.Length == 0 ||
                referenceTargets == null)
            {
                failure = "Profile requires fixtures, metric definitions and a non-null target list.";
                return false;
            }

            var fixtureIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fixtures.Length; index++)
            {
                VehicleCalibrationFixture fixture = fixtures[index];
                if (fixture == null)
                {
                    failure = $"Calibration fixture at index {index} is null.";
                    return false;
                }

                if (!fixture.Validate(out failure))
                {
                    failure = $"Invalid fixture at index {index}: {failure}";
                    return false;
                }

                if (!fixtureIds.Add(fixture.FixtureId))
                {
                    failure = $"Duplicate calibration fixture ID '{fixture.FixtureId}'.";
                    return false;
                }
            }

            var metricIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < metricDefinitions.Length; index++)
            {
                VehicleMetricDefinition definition = metricDefinitions[index];
                if (definition == null)
                {
                    failure = $"Metric definition at index {index} is null.";
                    return false;
                }

                if (!definition.Validate(out failure))
                {
                    failure = $"Invalid metric definition at index {index}: {failure}";
                    return false;
                }

                if (!metricIds.Add(definition.MetricId))
                {
                    failure = $"Duplicate metric definition ID '{definition.MetricId}'.";
                    return false;
                }
            }

            for (int fixtureIndex = 0; fixtureIndex < fixtures.Length; fixtureIndex++)
            {
                VehicleCalibrationFixture fixture = fixtures[fixtureIndex];
                if (!fixture.CanRun)
                {
                    continue;
                }

                string[] fixtureMetricIds = fixture.MetricIds;
                for (int metricIndex = 0; metricIndex < fixtureMetricIds.Length; metricIndex++)
                {
                    if (!metricIds.Contains(fixtureMetricIds[metricIndex]))
                    {
                        failure = $"Fixture '{fixture.FixtureId}' references unknown metric '{fixtureMetricIds[metricIndex]}'.";
                        return false;
                    }
                }
            }

            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < referenceTargets.Length; index++)
            {
                VehicleReferenceTarget target = referenceTargets[index];
                if (target == null)
                {
                    failure = $"Reference target at index {index} is null.";
                    return false;
                }

                if (!target.Validate(out failure))
                {
                    failure = $"Invalid reference target at index {index}: {failure}";
                    return false;
                }

                if (!fixtureIds.Contains(target.FixtureId) || !metricIds.Contains(target.MetricId))
                {
                    failure = $"Reference target '{target.FixtureId}/{target.MetricId}' references an unknown fixture or metric.";
                    return false;
                }

                string key = target.FixtureId + "\n" + target.MetricId;
                if (!targetKeys.Add(key))
                {
                    failure = $"Duplicate reference target '{target.FixtureId}/{target.MetricId}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
