using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Editor.VehicleSimulation;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleValidation
{
    public static class VehiclePhysicsValidationEvidenceExporter
    {
        private const float FixedStepSeconds = 0.02f;
        private const string PhysicsEvidence =
            "Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json";

        [MenuItem(VehiclePhysicsValidationPaths.MenuRoot + "Export Summary CSV")]
        public static void Run()
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            VehicleCalibrationProfile profile =
                AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                    VehiclePhysicsValidationPaths.Profile);
            if (config == null || profile == null)
            {
                throw new InvalidOperationException(
                    "Build the M06A validation setup before exporting evidence.");
            }

            if (!config.Validate(out string configFailure))
            {
                throw new InvalidOperationException(
                    "M06A vehicle config is invalid: " + configFailure);
            }

            if (!profile.Validate(out string profileFailure))
            {
                throw new InvalidOperationException(
                    "M06A calibration profile is invalid: " + profileFailure);
            }

            Directory.CreateDirectory(Path.GetFullPath(VehiclePhysicsValidationPaths.DocumentationRoot));
            PureStartIdleEvidence pure = RunPureStartIdle(config, profile, 5);
            PhysicsRunEvidenceDto physics = ReadPhysicsEvidence(config, profile);
            WriteTargets(profile);
            WriteRuns(profile, pure, physics);
            WriteMetricResults(profile, config, pure, physics);
            WriteTuningLog();
            AssetDatabase.Refresh();
            Debug.Log(
                "M06A_PHYSICS_VALIDATION_EXPORT_OK " +
                $"pureTrials={pure.startSeconds.Length} physxEvidence={(physics != null && physics.passed)} " +
                $"output={Path.GetFullPath(VehiclePhysicsValidationPaths.DocumentationRoot)}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06A evidence export batch entry requires batch mode.");
            }

            Run();
        }

        private static PureStartIdleEvidence RunPureStartIdle(
            VehicleSimulationConfig config,
            VehicleCalibrationProfile profile,
            int trialCount)
        {
            if (!profile.TryGetFixture("start-idle", out VehicleCalibrationFixture fixture))
            {
                throw new InvalidOperationException("M06A profile has no start-idle fixture.");
            }

            const string runId = "m06a-pure-start-idle";
            var starts = new float[trialCount];
            var idles = new float[trialCount];
            var idleDeviations = new float[trialCount];
            var trials = new VehicleCalibrationTrial[trialCount];
            for (int trial = 0; trial < trialCount; trial++)
            {
                var root = new VehicleSimulationRoot(
                    config,
                    new FixedCalibrationBackend(config),
                    new AvailablePrerequisites());
                VehicleInputState crank = new VehicleInputState(
                    0f, 1f, 0f, 0f, true, true, false, 0);
                int crankTicks = 0;
                while (root.State.EngineStatus != VehicleEngineStatus.Running && crankTicks < 300)
                {
                    root.Tick(FixedStepSeconds, crank);
                    crankTicks++;
                }

                if (root.State.EngineStatus != VehicleEngineStatus.Running)
                {
                    throw new InvalidOperationException(
                        $"M06A pure calibration trial {trial + 1} failed to start.");
                }

                starts[trial] = crankTicks * FixedStepSeconds;
                VehicleInputState idle = new VehicleInputState(
                    0f, 1f, 0f, 0f, true, false, false, 0);
                const int idleSettleTicks = 100;
                for (int tick = 0; tick < idleSettleTicks; tick++)
                {
                    root.Tick(FixedStepSeconds, idle);
                }

                const int idleMeasurementTicks = 100;
                float idleRpmSum = 0f;
                float maximumDeviation = 0f;
                for (int tick = 0; tick < idleMeasurementTicks; tick++)
                {
                    root.Tick(FixedStepSeconds, idle);
                    idleRpmSum += root.State.EngineRpm;
                    maximumDeviation = Mathf.Max(
                        maximumDeviation,
                        Mathf.Abs(root.State.EngineRpm - config.Engine.IdleTargetRpm));
                }

                if (!root.State.IsFinite() || !root.LastTickWasFinite)
                {
                    throw new InvalidOperationException(
                        $"M06A pure calibration trial {trial + 1} produced invalid state.");
                }

                idles[trial] = idleRpmSum / idleMeasurementTicks;
                idleDeviations[trial] = maximumDeviation;
                trials[trial] = new VehicleCalibrationTrial(
                    trial + 1,
                    0,
                    VehicleCalibrationTrialStatus.Completed,
                    root.State.ElapsedSeconds,
                    new[]
                    {
                        Observation("engine.starter_time_s", starts[trial]),
                        Observation("engine.idle_rpm", idles[trial]),
                        Observation("engine.idle_max_deviation_rpm", idleDeviations[trial]),
                        Observation("numeric.finite_state", 1f)
                    },
                    "Deterministic FixedCalibrationBackend trial; seed is not used.");
            }

            VehicleMetricResult[] results =
            {
                CompareProfileMetric(profile, fixture, runId, "engine.starter_time_s", starts),
                CompareProfileMetric(profile, fixture, runId, "engine.idle_rpm", idles),
                CompareProfileMetric(
                    profile,
                    fixture,
                    runId,
                    "engine.idle_max_deviation_rpm",
                    idleDeviations),
                CompareProfileMetric(
                    profile,
                    fixture,
                    runId,
                    "numeric.finite_state",
                    Enumerable.Repeat(1f, trialCount).ToArray())
            };
            string startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var run = new VehicleCalibrationRun(
                runId,
                profile.ProfileId,
                fixture.FixtureId,
                startedUtc,
                VehicleCalibrationRunStatus.Running,
                fixture.Environment);
            run.Complete(
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                trials,
                results);
            bool runIsValid = run.Validate(out string runFailure);
            if (run.Status != VehicleCalibrationRunStatus.Passed || !runIsValid)
            {
                throw new InvalidOperationException(
                    "M06A pure calibration run did not pass its declared profile targets: " +
                    runFailure);
            }

            return new PureStartIdleEvidence(
                starts,
                idles,
                idleDeviations,
                VehicleCalibrationComparator.Summarize(starts),
                VehicleCalibrationComparator.Summarize(idles),
                VehicleCalibrationComparator.Summarize(idleDeviations),
                run);
        }

        private static VehicleMetricTrialObservation Observation(string metricId, float value)
        {
            return new VehicleMetricTrialObservation(
                metricId,
                VehicleCalibrationComparator.Summarize(new[] { value }));
        }

        private static VehicleMetricResult CompareProfileMetric(
            VehicleCalibrationProfile profile,
            VehicleCalibrationFixture fixture,
            string runId,
            string metricId,
            float[] samples)
        {
            if (!profile.TryGetMetricDefinition(metricId, out VehicleMetricDefinition definition) ||
                !profile.TryGetReferenceTarget(fixture.FixtureId, metricId, out VehicleReferenceTarget target))
            {
                throw new InvalidOperationException(
                    $"M06A profile cannot compare '{fixture.FixtureId}/{metricId}'.");
            }

            return VehicleCalibrationComparator.Compare(
                runId,
                definition,
                target,
                samples,
                fixture.RepeatedRunTolerance01);
        }

        private static void WriteTargets(VehicleCalibrationProfile profile)
        {
            var rows = new List<string[]>
            {
                new[]
                {
                    "target_id", "fixture_id", "metric_id", "classification", "status",
                    "mode", "target_value", "minimum_value", "maximum_value", "unit",
                    "absolute_tolerance", "relative_tolerance_01", "confidence_01", "source", "notes"
                }
            };
            for (int index = 0; index < profile.ReferenceTargets.Length; index++)
            {
                VehicleReferenceTarget target = profile.ReferenceTargets[index];
                profile.TryGetMetricDefinition(target.MetricId, out VehicleMetricDefinition definition);
                bool unknown = target.Status == VehicleReferenceTargetStatus.Unknown ||
                               target.Status == VehicleReferenceTargetStatus.Blocked;
                rows.Add(new[]
                {
                    $"{target.FixtureId}:{target.MetricId}", target.FixtureId, target.MetricId,
                    target.Classification.ToString(), target.Status.ToString(), target.Mode.ToString(),
                    unknown ? string.Empty : Number(target.TargetValue),
                    unknown ? string.Empty : Number(target.MinimumValue),
                    unknown ? string.Empty : Number(target.MaximumValue), definition?.Unit ?? string.Empty,
                    unknown ? string.Empty : Number(target.ResolveAbsoluteTolerance(definition)),
                    unknown ? string.Empty : Number(target.ResolveRelativeTolerance(definition)),
                    unknown ? string.Empty : Number(target.Confidence01), target.Source, target.Notes
                });
            }

            rows.Add(ReferenceTargetRow(
                "reference:donor-root-rigidbody-mass", "mass.donor_root_rigidbody_component_kg",
                "MeasuredDonorReference", "EvidenceOnly", "389", "kg",
                "ReferenceCaptureDatabase:3041151e93c71a87d89d19779615dc68",
                "Exact serialized component value; donor object is kinematic; not curb/assembled mass."));
            rows.Add(ReferenceTargetRow(
                "reference:body-aabb-width", "geometry.body_mesh_aabb_width_m",
                "MeasuredDonorReference", "Available", "1.465916", "m",
                "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a",
                "datsun_body mesh only; not assembled envelope."));
            rows.Add(ReferenceTargetRow(
                "reference:body-aabb-height", "geometry.body_mesh_aabb_height_m",
                "MeasuredDonorReference", "Available", "1.1102937", "m",
                "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a",
                "datsun_body mesh only; not assembled envelope."));
            rows.Add(ReferenceTargetRow(
                "reference:body-aabb-length", "geometry.body_mesh_aabb_length_m",
                "MeasuredDonorReference", "Available", "3.5462036", "m",
                "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a",
                "datsun_body mesh only; not assembled envelope."));
            string[] missing =
            {
                "mass.total_assembled_kg", "mass.donor_part_contributions_kg", "mass.donor_center_of_mass_m",
                "wheel.fitted_radius_m", "geometry.donor_ride_height_m", "geometry.donor_ground_clearance_m",
                "engine.donor_start_idle_stall", "gearbox.donor_ratios", "drivetrain.donor_final_drive",
                "longitudinal.donor_acceleration", "longitudinal.donor_braking",
                "lateral.donor_steering", "suspension.donor_response"
            };
            for (int index = 0; index < missing.Length; index++)
            {
                rows.Add(ReferenceTargetRow(
                    "unknown:" + missing[index], missing[index], "Unknown", "Missing", string.Empty,
                    string.Empty, "Docs/ReferenceCapture/MISSING_REFERENCE_DATA.csv",
                    "No controlled donor target; must remain blank/Unknown."));
            }

            WriteCsv("CALIBRATION_TARGETS.csv", rows);
        }

        private static string[] ReferenceTargetRow(
            string targetId,
            string metric,
            string classification,
            string status,
            string value,
            string unit,
            string source,
            string notes)
        {
            return new[]
            {
                targetId, "reference-only", metric, classification, status, "Informational",
                value, string.Empty, string.Empty, unit, string.Empty, string.Empty, string.Empty,
                source, notes
            };
        }

        private static void WriteRuns(
            VehicleCalibrationProfile profile,
            PureStartIdleEvidence pure,
            PhysicsRunEvidenceDto physics)
        {
            var rows = new List<string[]>
            {
                new[]
                {
                    "run_id", "fixture_id", "status", "trial_count", "execution_mode",
                    "fixed_timestep_s", "substeps", "unity_version", "hardware", "route",
                    "started_utc", "evidence_path", "notes"
                },
                new[]
                {
                    pure.run.RunId, "start-idle", pure.run.Status.ToString(), pure.startSeconds.Length.ToString(),
                    "EditModePure", Number(FixedStepSeconds), profile.VehicleConfiguration.SubstepCount.ToString(),
                    Application.unityVersion, Hardware(), "FixedCalibrationBackend", pure.run.StartedUtc,
                    "generated in exporter", "Deterministic project baseline; not donor/PhysX parity."
                },
                new[]
                {
                    "m06a-static-geometry-mass", "static-geometry-mass", "Inconclusive", "1",
                    "EditModeStatic", Number(FixedStepSeconds), profile.VehicleConfiguration.SubstepCount.ToString(),
                    Application.unityVersion, Hardware(), "Profile/config snapshot", DateTime.UtcNow.ToString("O"),
                    "generated in exporter", "Explicit project/reference snapshot; unavailable donor values remain Unknown."
                }
            };
            AddStaticRunRow(rows, profile, "m06a-static-powertrain-config", "powertrain-config",
                "Central M06 engine/clutch/gearbox config snapshot.");
            AddStaticRunRow(rows, profile, "m06a-static-surface-comparison", "surface-comparison",
                "Project-owned surface coefficient snapshot; dynamic evidence is a separate PhysX run.");
            AddStaticRunRow(rows, profile, "m06a-static-garage-clearance", "garage-clearance",
                "Project-authored opening/envelope/threshold measurements.");
            AddStaticRunRow(rows, profile, "m06a-reference-reference-only", "reference-only",
                "Reference availability and explicit Unknown records.", "Inconclusive");

            for (int index = 0; index < profile.Fixtures.Length; index++)
            {
                VehicleCalibrationFixture fixture = profile.Fixtures[index];
                if (fixture.FixtureId == "static-geometry-mass")
                {
                    continue;
                }

                bool hasPhysics = physics != null && HasFixtureEvidence(physics, fixture.FixtureId);
                bool hasUnknownTargets = HasUnknownTargets(profile, fixture.FixtureId);
                rows.Add(new[]
                {
                    "m06a-physx-" + fixture.FixtureId, fixture.FixtureId,
                    hasPhysics ? (hasUnknownTargets ? "Inconclusive" : "Passed") : "NotRun",
                    fixture.TrialCount.ToString(), "PlayModePhysX", Number(Time.fixedDeltaTime),
                    profile.VehicleConfiguration.SubstepCount.ToString(),
                    physics?.unityVersion ?? Application.unityVersion,
                    physics == null ? Hardware() :
                        $"{physics.operatingSystem}; {physics.processorType}; {physics.graphicsDeviceName}",
                    fixture.Environment.SceneOrRouteId, physics?.capturedUtc ?? string.Empty,
                    hasPhysics ? PhysicsEvidence : string.Empty,
                    hasPhysics
                        ? (hasUnknownTargets
                            ? "Execution passed; one or more declared reference targets remain Unknown."
                            : "Fresh passing PhysX evidence.")
                        : "No passing durable PhysX evidence was available when CSV was exported."
                });
            }

            WriteCsv("CALIBRATION_RUNS.csv", rows);
        }

        private static bool HasUnknownTargets(
            VehicleCalibrationProfile profile,
            string fixtureId)
        {
            for (int index = 0; index < profile.ReferenceTargets.Length; index++)
            {
                VehicleReferenceTarget target = profile.ReferenceTargets[index];
                if (string.Equals(target.FixtureId, fixtureId, StringComparison.Ordinal) &&
                    (target.Status == VehicleReferenceTargetStatus.Unknown ||
                     target.Status == VehicleReferenceTargetStatus.Blocked))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFixtureEvidence(
            PhysicsRunEvidenceDto physics,
            string fixtureId)
        {
            switch (fixtureId)
            {
                case "start-idle":
                    return physics.trials != null && physics.trials.Length == 3 &&
                           physics.trials.All(trial =>
                               VehicleSimulationMath.IsFinite(trial.startSeconds) &&
                               trial.startSeconds >= 0.1f && trial.startSeconds <= 1f &&
                               VehicleSimulationMath.IsFinite(trial.idleAverageRpm) &&
                               trial.idleAverageRpm >= 780f && trial.idleAverageRpm <= 1020f &&
                               VehicleSimulationMath.IsFinite(trial.idleMaximumDeviationRpm) &&
                               trial.idleMaximumDeviationRpm <= 120f);
                case "acceleration":
                    return physics.trials != null && physics.trials.Length == 3 &&
                           physics.trials.All(trial =>
                               VehicleSimulationMath.IsFinite(trial.launchPeakSpeedMetersPerSecond) &&
                               VehicleSimulationMath.IsFinite(trial.launchDistanceMeters) &&
                               trial.launchPeakSpeedMetersPerSecond > 0.5f &&
                               trial.launchDistanceMeters > 0.4f);
                case "braking":
                    return physics.trials != null && physics.trials.Length == 3 &&
                           physics.trials.All(trial =>
                               trial.brakingInitialSpeedMetersPerSecond > 0f &&
                               trial.brakingFinalSpeedMetersPerSecond /
                                   trial.brakingInitialSpeedMetersPerSecond <= 0.25f &&
                               trial.brakingDistanceMeters > 0f &&
                               trial.brakingDistanceMeters <= 25f &&
                               trial.maximumBrakingSpeedIncreaseMetersPerSecond <= 0.15f &&
                               trial.minimumBrakingContactWheelCount >= 2);
                case "coastdown":
                    return physics.coastdownTrials != null && physics.coastdownTrials.Length == 3 &&
                           physics.coastdownTrials.All(trial =>
                               trial.speedLossMetersPerSecond > 0.02f &&
                               trial.maximumSpeedMetersPerSecond - trial.initialSpeedMetersPerSecond <= 0.15f);
                case "steering-slalom":
                    return physics.steering != null &&
                           physics.steering.maximumPositiveSteeringAngleDegrees > 2f &&
                           physics.steering.minimumNegativeSteeringAngleDegrees < -2f &&
                           physics.steering.minimumContactWheelCount >= 2 &&
                           physics.steering.maximumZeroContactStreakFrames <= 5;
                case "suspension-bump":
                    return physics.bump != null &&
                           physics.bump.peakCompression01 -
                               physics.bump.baselineAverageCompression01 > 0.1f &&
                           Mathf.Abs(physics.bump.recoveredAverageCompression01 -
                               physics.bump.baselineAverageCompression01) <= 0.15f &&
                           physics.bump.minimumBumpContactWheelCount >= 2;
                case "surface-comparison":
                    if (physics.surfaces == null || physics.surfaces.Length < 4 ||
                        physics.surfaces.Any(surface =>
                            surface.contactWheelCount < 2 ||
                            !VehicleSimulationMath.IsFinite(
                                surface.coastdownLossMetersPerSecond) ||
                            surface.coastdownLossMetersPerSecond <= 0f))
                    {
                        return false;
                    }

                    for (int index = 1; index < physics.surfaces.Length; index++)
                    {
                        if (physics.surfaces[index - 1].frictionMultiplier <=
                                physics.surfaces[index].frictionMultiplier ||
                            physics.surfaces[index - 1].rollingResistanceMultiplier >=
                                physics.surfaces[index].rollingResistanceMultiplier)
                        {
                            return false;
                        }
                    }

                    return true;
                case "hill-start":
                    return physics.hill != null &&
                           Mathf.Abs(physics.hill.slopeDegrees - 6f) <= 0.01f &&
                           physics.hill.holdDriftMeters <= 0.25f &&
                           physics.hill.uphillProgressMeters > 0.5f &&
                           physics.hill.minimumContactWheelCount >= 2;
                case "garage-clearance":
                    return physics.productionWorld != null &&
                           physics.productionWorld.garageDoorsOpen &&
                           physics.productionWorld.drivewayGatesOpen &&
                           physics.productionWorld.minimumContactWheelCount >= 2 &&
                           physics.productionWorld.sawPavedSurface;
                case "world-transition":
                    return physics.productionWorld != null &&
                           physics.productionWorld.garageDoorsOpen &&
                           physics.productionWorld.drivewayGatesOpen &&
                           !physics.productionWorld.nextCellInitiallyLoaded &&
                           physics.productionWorld.nextCellLoaded &&
                           physics.productionWorld.horizontalProgressMeters >= 12.23f &&
                           physics.productionWorld.maximumLateralDeviationMeters <= 1.1f &&
                           physics.productionWorld.maximumZeroContactStreakFrames <= 8 &&
                           physics.productionWorld.minimumStreamingContactWheelCount >= 2 &&
                           physics.productionWorld.maximumStreamingVerticalDeltaMeters <= 0.35f &&
                           physics.productionWorld.nextCellOnlyMinimumContactWheelCount >= 2 &&
                           physics.productionWorld.nextCellOnlyMaximumVerticalDeltaMeters <= 0.35f &&
                           physics.productionWorld.postStreamingContactWheelCount >= 2 &&
                           physics.productionWorld.postStreamingVerticalDeltaMeters <= 0.35f &&
                           physics.productionWorld.sawPavedSurface &&
                           physics.productionWorld.sawGravelSurface &&
                           physics.productionWorld.sawNextCellGrassSurface;
                default:
                    return false;
            }
        }

        private static void AddStaticRunRow(
            List<string[]> rows,
            VehicleCalibrationProfile profile,
            string runId,
            string fixtureId,
            string notes,
            string status = "Passed")
        {
            rows.Add(new[]
            {
                runId, fixtureId, status, "1", "EditModeStatic",
                Number(FixedStepSeconds), profile.VehicleConfiguration.SubstepCount.ToString(),
                Application.unityVersion, Hardware(), "Profile/config/reference snapshot",
                DateTime.UtcNow.ToString("O"), "generated in exporter", notes
            });
        }

        private static void WriteMetricResults(
            VehicleCalibrationProfile profile,
            VehicleSimulationConfig config,
            PureStartIdleEvidence pure,
            PhysicsRunEvidenceDto physics)
        {
            var rows = new List<string[]>
            {
                new[]
                {
                    "result_id", "run_id", "fixture_id", "metric_id", "classification", "status",
                    "sample_count", "mean", "median", "minimum", "maximum", "stddev", "target",
                    "accepted_minimum", "accepted_maximum", "unit", "source", "notes"
                }
            };
            AddValue(rows, "static", "static-geometry-mass", "mass.proxy_total_kg", "RemakeDesignTarget", "Passed", config.Dynamics.ProvisionalMassKilograms, config.Dynamics.ProvisionalMassKilograms, config.Dynamics.ProvisionalMassKilograms - 0.01f, config.Dynamics.ProvisionalMassKilograms + 0.01f, "kg", "M06 config", "Not donor total mass.");
            AddValue(rows, "static", "static-geometry-mass", "mass.logical_parts_sum_kg", "RemakeDesignTarget", "Informational", 871.7f, 871.7f, 871.69f, 871.71f, "kg", "M06 logical assembly", "Excluded from Rigidbody mass.");
            AddLogicalPartRows(rows, profile);
            AddValue(rows, "static", "static-geometry-mass", "mass.center_of_mass_x_m", "RemakeDesignTarget", "Passed", config.Dynamics.CenterOfMassMeters.x, config.Dynamics.CenterOfMassMeters.x, config.Dynamics.CenterOfMassMeters.x - 0.001f, config.Dynamics.CenterOfMassMeters.x + 0.001f, "m", "M06A config migration", "Explicit proxy CoM X; donor assembled CoM is Unknown.");
            AddValue(rows, "static", "static-geometry-mass", "mass.center_of_mass_y_m", "RemakeDesignTarget", "Passed", config.Dynamics.CenterOfMassMeters.y, config.Dynamics.CenterOfMassMeters.y, config.Dynamics.CenterOfMassMeters.y - 0.001f, config.Dynamics.CenterOfMassMeters.y + 0.001f, "m", "M06A config migration", "Ownership moved from implicit collider result to central config; numeric value unchanged.");
            AddValue(rows, "static", "static-geometry-mass", "mass.center_of_mass_z_m", "RemakeDesignTarget", "Passed", config.Dynamics.CenterOfMassMeters.z, config.Dynamics.CenterOfMassMeters.z, config.Dynamics.CenterOfMassMeters.z - 0.001f, config.Dynamics.CenterOfMassMeters.z + 0.001f, "m", "M06A config migration", "Explicit proxy CoM Z; donor assembled CoM is Unknown.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.wheelbase_m", "DerivedReference", "Passed", 2.334f, 2.334f, 2.332f, 2.336f, "m", "ReferenceCaptureDatabase:a2a2647de4c9f2e05568e98a27d59380", "Wheel-root derivation.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.front_track_m", "DerivedReference", "Passed", 1.2600002f, 1.2600002f, 1.2580002f, 1.2620002f, "m", "ReferenceCaptureDatabase:93b15a1420a962441f82ad53e074ed09", "At wheel roots.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.rear_track_m", "DerivedReference", "Passed", 1.2060003f, 1.2060003f, 1.2040003f, 1.2080003f, "m", "ReferenceCaptureDatabase:98bbe4fc8f0e5702bfbbbf03592d0b28", "At wheel roots.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.wheel_radius_m", "PlausibilityTarget", "Informational", config.Dynamics.WheelRadiusMeters, 0.272667f, 0.271667f, 0.273667f, "m", "ReferenceCaptureDatabase:f7d49044cec918b66ac64e0099a7781f", "Candidate mesh radius; fitted identity unknown.");

            float staticCompression = config.Dynamics.ProvisionalMassKilograms * 9.81f /
                                      config.WheelCount /
                                      config.Dynamics.SpringRateNewtonPerMeter;
            float anchorHeight = config.Dynamics.WheelRadiusMeters +
                                 config.Dynamics.SuspensionRestLengthMeters -
                                 staticCompression;
            float colliderClearance = anchorHeight + config.Dynamics.ChassisColliderCenterMeters.y -
                                      config.Dynamics.ChassisColliderSizeMeters.y * 0.5f;
            AddValue(rows, "static", "static-geometry-mass", "suspension.design_static_compression_m", "RemakeDesignTarget", "Informational", staticCompression, staticCompression, staticCompression, staticCompression, "m", "M06 config calculation", "Not donor static compression.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.proxy_anchor_height_m", "RemakeDesignTarget", "Informational", anchorHeight, anchorHeight, anchorHeight, anchorHeight, "m", "M06 reset-pose calculation", "Not donor ride height.");
            AddValue(rows, "static", "static-geometry-mass", "geometry.proxy_collider_clearance_m", "RemakeDesignTarget", "Informational", colliderClearance, colliderClearance, colliderClearance, colliderClearance, "m", "M06 proxy calculation", "High provisional collider clearance; donor ground clearance Unknown.");
            AddUnknown(rows, "static", "static-geometry-mass", "geometry.ride_height_m", "m", "No controlled donor fixture.");
            AddUnknown(rows, "static", "static-geometry-mass", "geometry.ground_clearance_m", "m", "No validated chassis marker.");
            AddValue(rows, "static", "static-geometry-mass", "suspension.rest_length_config_m", "RemakeDesignTarget", "Informational", config.Dynamics.SuspensionRestLengthMeters, config.Dynamics.SuspensionRestLengthMeters, config.Dynamics.SuspensionRestLengthMeters, config.Dynamics.SuspensionRestLengthMeters, "m", "M06 config", "Configured raycast suspension rest length; not donor rest position.");
            AddValue(rows, "static", "static-geometry-mass", "suspension.travel_config_m", "RemakeDesignTarget", "Informational", config.Dynamics.SuspensionTravelMeters, config.Dynamics.SuspensionTravelMeters, config.Dynamics.SuspensionTravelMeters, config.Dynamics.SuspensionTravelMeters, "m", "M06 config", "Donor suspension travel Missing.");
            AddValue(rows, "static", "static-geometry-mass", "suspension.spring_rate_config_n_m", "RemakeDesignTarget", "Informational", config.Dynamics.SpringRateNewtonPerMeter, config.Dynamics.SpringRateNewtonPerMeter, config.Dynamics.SpringRateNewtonPerMeter, config.Dynamics.SpringRateNewtonPerMeter, "N/m", "M06 config", "Project tuning; donor spring curve Missing.");
            AddValue(rows, "static", "static-geometry-mass", "suspension.damper_rate_config_n_s_m", "RemakeDesignTarget", "Informational", config.Dynamics.DamperRateNewtonSecondsPerMeter, config.Dynamics.DamperRateNewtonSecondsPerMeter, config.Dynamics.DamperRateNewtonSecondsPerMeter, config.Dynamics.DamperRateNewtonSecondsPerMeter, "N*s/m", "M06 config", "Project tuning; donor damping/rebound Missing.");

            for (int resultIndex = 0; resultIndex < pure.run.MetricResults.Length; resultIndex++)
            {
                AddMetricResultRow(
                    rows,
                    profile,
                    pure.run.MetricResults[resultIndex],
                    "M06A pure VehicleCalibrationRun",
                    "Durable result produced by VehicleCalibrationComparator.");
            }
            AddValue(rows, "static", "static-geometry-mass", "geometry.steering_lock_config_deg", "RemakeDesignTarget", "Informational", config.Dynamics.MaximumSteeringAngleDegrees, config.Dynamics.MaximumSteeringAngleDegrees, config.Dynamics.MaximumSteeringAngleDegrees, config.Dynamics.MaximumSteeringAngleDegrees, "deg", "M06 config", "Donor lock Missing.");
            AddValue(rows, "static", "static-geometry-mass", "brake.uniform_front_share", "RemakeDesignTarget", "Informational", 0.5f, 0.5f, 0.5f, 0.5f, "ratio", "M06 wheel command routing", "Uniform four-wheel command; not donor balance.");
            AddValue(rows, "static", "garage-clearance", "world.garage_width_margin_m", "RemakeDesignTarget", "Passed", 0.92f, 0.92f, 0.92f, float.PositiveInfinity, "m", "05A representative envelope fixture", "3.12m opening minus 2.20m representative vehicle envelope; not current proxy collider width.");
            AddValue(rows, "static", "garage-clearance", "world.garage_height_margin_m", "RemakeDesignTarget", "Passed", 0.47f, 0.47f, 0.47f, float.PositiveInfinity, "m", "05A representative envelope fixture", "2.22m opening minus 1.75m representative vehicle envelope; not current proxy collider height.");
            AddValue(rows, "static", "garage-clearance", "world.threshold_height_m", "RemakeDesignTarget", "Informational", 0.105f, 0.105f, 0.095f, 0.115f, "m", "Production cell project measurement", "Not donor threshold parity.");
            AddPowertrainConfigRows(rows, config);
            AddGearRows(rows, config);
            AddSurfaceRows(rows, config);
            AddPhysicsRows(rows, profile, physics, config);
            AddValue(rows, "reference", "reference-only", "mass.donor_root_rigidbody_component_kg", "MeasuredDonorReference", "Informational", 389f, 389f, 389f, 389f, "kg", "ReferenceCaptureDatabase:3041151e93c71a87d89d19779615dc68", "Exact kinematic donor component value; not comparable to curb or assembled mass.");
            AddValue(rows, "reference", "reference-only", "geometry.body_mesh_aabb_width_m", "MeasuredDonorReference", "Informational", 1.465916f, 1.465916f, 1.465916f, 1.465916f, "m", "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a", "Body mesh only; not assembled envelope.");
            AddValue(rows, "reference", "reference-only", "geometry.body_mesh_aabb_height_m", "MeasuredDonorReference", "Informational", 1.1102937f, 1.1102937f, 1.1102937f, 1.1102937f, "m", "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a", "Body mesh only; not assembled envelope.");
            AddValue(rows, "reference", "reference-only", "geometry.body_mesh_aabb_length_m", "MeasuredDonorReference", "Informational", 3.5462036f, 3.5462036f, 3.5462036f, 3.5462036f, "m", "ReferenceCaptureDatabase:5b658bae6d89917e377db64110842c6a", "Body mesh only; not assembled envelope.");
            AddUnknownDynamicRows(rows);
            WriteCsv("METRIC_RESULTS.csv", rows);
        }

        private static void AddLogicalPartRows(
            List<string[]> rows,
            VehicleCalibrationProfile profile)
        {
            if (!profile.TryGetFixture(
                    "static-geometry-mass",
                    out VehicleCalibrationFixture fixture) ||
                fixture.Environment?.Mass?.InstalledParts == null)
            {
                AddUnknown(rows, "static", "static-geometry-mass",
                    "mass.logical_part_contributions_kg", "kg",
                    "Calibration profile did not expose logical part records.");
                return;
            }

            VehicleCalibrationPartMass[] parts = fixture.Environment.Mass.InstalledParts;
            for (int index = 0; index < parts.Length; index++)
            {
                VehicleCalibrationPartMass part = parts[index];
                if (part == null)
                {
                    continue;
                }

                AddValue(rows, "static", "static-geometry-mass",
                    "mass.logical_part." + part.PartDefinitionId + "_kg",
                    "RemakeDesignTarget", "Informational", part.MassKilograms,
                    part.MassKilograms, part.MassKilograms, part.MassKilograms, "kg",
                    "M06 logical AssemblyGraph",
                    $"installed={part.Installed}; includedInPhysicsMass={part.IncludedInPhysicsMass}; " +
                    "excluded from Rigidbody mass calibration.");
            }
        }

        private static void AddPowertrainConfigRows(
            List<string[]> rows,
            VehicleSimulationConfig config)
        {
            AddValue(rows, "static", "powertrain-config", "engine.throttle_response_config_per_s",
                "RemakeDesignTarget", "Informational", config.Engine.ThrottleResponsePerSecond,
                config.Engine.ThrottleResponsePerSecond, config.Engine.ThrottleResponsePerSecond,
                config.Engine.ThrottleResponsePerSecond, "1/s", "M06 config",
                "Configured input smoothing; free-rev donor response Missing.");
            AddValue(rows, "static", "powertrain-config", "engine.stall_rpm_config",
                "RemakeDesignTarget", "Informational", config.Engine.StallRpm,
                config.Engine.StallRpm, config.Engine.StallRpm, config.Engine.StallRpm,
                "rpm", "M06 config", "Config value; donor stall fixture Missing.");
            AddValue(rows, "static", "powertrain-config", "engine.braking_torque_config_nm",
                "RemakeDesignTarget", "Informational", config.Engine.EngineBrakingNewtonMeters,
                config.Engine.EngineBrakingNewtonMeters, config.Engine.EngineBrakingNewtonMeters,
                config.Engine.EngineBrakingNewtonMeters, "N*m", "M06 config",
                "Config value; controlled engine-braking run not executed.");
            AddValue(rows, "static", "powertrain-config", "clutch.maximum_torque_config_nm",
                "RemakeDesignTarget", "Informational", config.Clutch.MaximumTorqueNewtonMeters,
                config.Clutch.MaximumTorqueNewtonMeters, config.Clutch.MaximumTorqueNewtonMeters,
                config.Clutch.MaximumTorqueNewtonMeters, "N*m", "M06 config",
                "Config value; donor engagement curve Missing.");
            AddValue(rows, "static", "powertrain-config", "clutch.slip_stiffness_config",
                "RemakeDesignTarget", "Informational", config.Clutch.SlipStiffness,
                config.Clutch.SlipStiffness, config.Clutch.SlipStiffness,
                config.Clutch.SlipStiffness, "coefficient", "M06 config",
                "Config value; controlled clutch-slip fixture not executed.");
        }

        private static void AddGearRows(List<string[]> rows, VehicleSimulationConfig config)
        {
            const float speed = 10f;
            AddValue(rows, "static", "powertrain-config", "gearbox.reverse_ratio",
                "RemakeDesignTarget", "Informational", config.Gearbox.ReverseRatio,
                config.Gearbox.ReverseRatio, config.Gearbox.ReverseRatio,
                config.Gearbox.ReverseRatio, "ratio", "M06 config", "Donor ratio Missing.");
            AddValue(rows, "static", "powertrain-config", "drivetrain.final_drive_ratio",
                "RemakeDesignTarget", "Informational", config.Gearbox.FinalDriveRatio,
                config.Gearbox.FinalDriveRatio, config.Gearbox.FinalDriveRatio,
                config.Gearbox.FinalDriveRatio, "ratio", "M06 config", "Donor final drive Missing.");
            for (int gear = 1; gear <= config.Gearbox.ForwardGearCount; gear++)
            {
                config.Gearbox.TryGetRatio(gear, out float ratio);
                AddValue(rows, "static", "powertrain-config", $"gearbox.forward_{gear}_ratio",
                    "RemakeDesignTarget", "Informational", ratio, ratio, ratio, ratio,
                    "ratio", "M06 config", "Donor ratio Missing.");
                float rpm = speed / config.Dynamics.WheelRadiusMeters *
                            ratio * config.Gearbox.FinalDriveRatio *
                            VehicleSimulationMath.RadiansPerSecondToRpm;
                AddValue(rows, "static", "static-geometry-mass", $"powertrain.rpm_at_10m_s_gear_{gear}",
                    "RemakeDesignTarget", "Passed", rpm, rpm, rpm - 0.1f, rpm + 0.1f, "rpm",
                    "M06 ratios/final drive/radius", "No-slip kinematic relation; donor ratios Missing.");
            }
        }

        private static void AddSurfaceRows(List<string[]> rows, VehicleSimulationConfig config)
        {
            VehicleSurfaceType[] types =
            {
                VehicleSurfaceType.Paved, VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt, VehicleSurfaceType.Grass, VehicleSurfaceType.MudWet
            };
            for (int index = 0; index < types.Length; index++)
            {
                VehicleSurfaceResponse response = config.GetSurfaceResponse(types[index]);
                AddValue(rows, "static", "surface-comparison", "surface.friction." + types[index],
                    "RemakeDesignTarget", "Informational", response.FrictionMultiplier,
                    response.FrictionMultiplier, response.FrictionMultiplier, response.FrictionMultiplier,
                    "multiplier", "M06 config", "Project surface ordering; not measured friction coefficient.");
            }
        }

        private static void AddPhysicsRows(
            List<string[]> rows,
            VehicleCalibrationProfile profile,
            PhysicsRunEvidenceDto physics,
            VehicleSimulationConfig config)
        {
            if (physics == null || !physics.passed)
            {
                for (int fixtureIndex = 0; fixtureIndex < profile.Fixtures.Length; fixtureIndex++)
                {
                    VehicleCalibrationFixture fixture = profile.Fixtures[fixtureIndex];
                    if (fixture.FixtureId == "static-geometry-mass")
                    {
                        continue;
                    }

                    for (int metricIndex = 0; metricIndex < fixture.MetricIds.Length; metricIndex++)
                    {
                        string metricId = fixture.MetricIds[metricIndex];
                        profile.TryGetMetricDefinition(metricId, out VehicleMetricDefinition definition);
                        AddNotRun(
                            rows,
                            fixture.FixtureId,
                            metricId,
                            "Fresh passing PlayMode evidence unavailable.",
                            definition?.Classification.ToString() ?? "Unknown");
                    }
                }

                return;
            }

            AddTrialAggregate(rows, physics.trials, "startSeconds",
                trial => trial.startSeconds,
                "start-idle", "engine.starter_time_s", "s", 0.1f, 1f);
            AddTrialAggregate(rows, physics.trials, "idleAverageRpm",
                trial => trial.idleAverageRpm,
                "start-idle", "engine.idle_rpm", "rpm", 780f, 1020f);
            AddTrialAggregate(rows, physics.trials, "idleMaximumDeviationRpm",
                trial => trial.idleMaximumDeviationRpm,
                "start-idle", "engine.idle_max_deviation_rpm", "rpm", 0f, 120f);
            AddTrialAggregate(rows, physics.trials, "launchPeakSpeedMetersPerSecond",
                trial => trial.launchPeakSpeedMetersPerSecond,
                "acceleration", "longitudinal.launch_peak_speed_m_s", "m/s", 0.5f, float.PositiveInfinity);
            AddTrialAggregate(rows, physics.trials, "launchDistanceMeters",
                trial => trial.launchDistanceMeters,
                "acceleration", "longitudinal.launch_distance_m", "m", 0.4f, float.PositiveInfinity);
            AddTrialAggregate(rows, physics.trials, "brakingFinalSpeedRatio",
                trial => trial.brakingInitialSpeedMetersPerSecond > 0f
                    ? trial.brakingFinalSpeedMetersPerSecond / trial.brakingInitialSpeedMetersPerSecond
                    : float.NaN,
                "braking", "longitudinal.braking_final_speed_ratio", "ratio", 0f, 0.25f);
            AddTrialAggregate(rows, physics.trials, "brakingDistanceMeters",
                trial => trial.brakingDistanceMeters,
                "braking", "longitudinal.braking_distance_m", "m", 0f, 25f);
            AddTrialAggregate(rows, physics.trials, "maximumBrakingSpeedIncrease",
                trial => trial.maximumBrakingSpeedIncreaseMetersPerSecond,
                "braking", "longitudinal.braking_max_speed_increase_m_s", "m/s", 0f, 0.15f);
            AddTrialAggregate(rows, physics.trials, "finalContactWheelCount",
                trial => trial.minimumBrakingContactWheelCount,
                "braking", "contact.minimum_wheel_count", "count", 2f, 4f);
            AddCoastdownRows(rows, physics.coastdownTrials);
            AddSteeringRows(rows, physics.steering, config);
            if (physics.bump != null)
            {
                AddValue(rows, "physx", "suspension-bump", "suspension.bump_peak_delta_01",
                    "RemakeDesignTarget", "Passed",
                    physics.bump.peakCompression01 - physics.bump.baselineAverageCompression01,
                    0.1f, 0.1f, 1f, "normalized", PhysicsEvidence, "80mm runtime bump.");
                AddValue(rows, "physx", "suspension-bump", "suspension.recovery_delta_01",
                    "RemakeDesignTarget", "Passed",
                    Mathf.Abs(physics.bump.recoveredAverageCompression01 - physics.bump.baselineAverageCompression01),
                    0f, 0f, 0.15f, "normalized", PhysicsEvidence, "Recovery after bump.");
                AddValue(rows, "physx", "suspension-bump", "contact.minimum_wheel_count",
                    "RemakeDesignTarget",
                    physics.bump.minimumBumpContactWheelCount >= 2 ? "Passed" : "Failed",
                    physics.bump.minimumBumpContactWheelCount, 2f, 2f, 4f, "count",
                    PhysicsEvidence, "Minimum contact count during the 80mm bump.");
            }

            if (physics.hill != null)
            {
                AddValue(rows, "physx", "hill-start", "longitudinal.hill_hold_drift_m",
                    "RemakeDesignTarget", physics.hill.holdDriftMeters <= 0.25f ? "Passed" : "Failed",
                    physics.hill.holdDriftMeters, 0.25f, 0f, 0.25f, "m", PhysicsEvidence,
                    "One-second brake hold on the project-authored six-degree slope.");
                AddValue(rows, "physx", "hill-start", "longitudinal.hill_start_progress_m",
                    "RemakeDesignTarget", physics.hill.uphillProgressMeters > 0.5f ? "Passed" : "Failed",
                    physics.hill.uphillProgressMeters, 0.5f, 0.5f, float.PositiveInfinity, "m",
                    PhysicsEvidence, "Three-second bounded uphill launch.");
                AddValue(rows, "physx", "hill-start", "contact.minimum_wheel_count",
                    "RemakeDesignTarget", physics.hill.minimumContactWheelCount >= 2 ? "Passed" : "Failed",
                    physics.hill.minimumContactWheelCount, 2f, 2f, 4f, "count", PhysicsEvidence,
                    "Minimum contacts during hill hold and launch.");
            }

            if (physics.surfaces != null && physics.surfaces.Length >= 4)
            {
                AddValue(rows, "physx", "surface-comparison", "surface.lookup_pass",
                    "RemakeDesignTarget", "Passed", 1f, 1f, 1f, 1f, "bool", PhysicsEvidence,
                    "Four typed surfaces resolved at contacts.");
                AddValue(rows, "physx", "surface-comparison", "surface.friction_ordering_pass",
                    "RemakeDesignTarget", "Passed", 1f, 1f, 1f, 1f, "bool",
                    PhysicsEvidence, "Configured friction multipliers are strictly ordered.");
                AddValue(rows, "physx", "surface-comparison",
                    "surface.rolling_resistance_ordering_pass", "RemakeDesignTarget", "Passed",
                    1f, 1f, 1f, 1f, "bool", PhysicsEvidence,
                    "Configured rolling-resistance multipliers are strictly ordered; measured coast-down " +
                    "values remain informational for the current prototype backend.");
                int minimumSurfaceContacts = physics.surfaces.Min(surface => surface.contactWheelCount);
                AddValue(rows, "physx", "surface-comparison", "contact.minimum_wheel_count",
                    "RemakeDesignTarget", minimumSurfaceContacts >= 2 ? "Passed" : "Failed",
                    minimumSurfaceContacts, 2f, 2f, 4f, "count", PhysicsEvidence,
                    "Minimum contacts across Paved/Gravel/Dirt/Grass samples.");
                for (int index = 0; index < physics.surfaces.Length; index++)
                {
                    SurfaceMetricDto surface = physics.surfaces[index];
                    AddValue(rows, "physx", "surface-comparison",
                        "surface.dynamic_coastdown_loss." + surface.surface.ToLowerInvariant() + "_m_s",
                        "RemakeDesignTarget", "Informational", surface.coastdownLossMetersPerSecond,
                        surface.coastdownLossMetersPerSecond, 0f, float.PositiveInfinity, "m/s",
                        PhysicsEvidence, "One-second unpowered dynamic surface response; not donor friction parity.");
                }
            }

            if (physics.productionWorld != null)
            {
                AddValue(rows, "physx", "world-transition", "world.streaming_neighbor_loaded",
                    "RemakeDesignTarget", physics.productionWorld.nextCellLoaded ? "Passed" : "Failed",
                    physics.productionWorld.nextCellLoaded ? 1f : 0f, 1f, 1f, 1f, "bool", PhysicsEvidence,
                    "Bounded production boundary only; full road contract remains open.");
                AddValue(rows, "physx", "world-transition", "world.route_progress_m",
                    "RemakeDesignTarget", "Passed", physics.productionWorld.horizontalProgressMeters,
                    12.23f, 12.23f, float.PositiveInfinity, "m", PhysicsEvidence,
                    "Garage start to streaming boundary.");
                AddValue(rows, "physx", "world-transition", "world.maximum_lateral_deviation_m",
                    "RemakeDesignTarget", "Passed", physics.productionWorld.maximumLateralDeviationMeters,
                    1.1f, 0f, 1.1f, "m", PhysicsEvidence, "Bounded corridor target.");
                AddValue(rows, "physx", "world-transition",
                    "contact.maximum_zero_contact_streak_frames", "RemakeDesignTarget",
                    physics.productionWorld.maximumZeroContactStreakFrames <= 8 ? "Passed" : "Failed",
                    physics.productionWorld.maximumZeroContactStreakFrames, 0f, 0f, 8f, "frames",
                    PhysicsEvidence, "Bounded garage/driveway/streaming traversal.");
                AddValue(rows, "physx", "world-transition",
                    "world.post_streaming_contact_wheel_count", "RemakeDesignTarget",
                    physics.productionWorld.postStreamingContactWheelCount >= 2 ? "Passed" : "Failed",
                    physics.productionWorld.postStreamingContactWheelCount, 2f, 2f, 4f, "count",
                    PhysicsEvidence, "Wheel contacts after automatic neighbor load completed.");
                AddValue(rows, "physx", "world-transition",
                    "world.post_streaming_vertical_delta_m", "RemakeDesignTarget",
                    physics.productionWorld.postStreamingVerticalDeltaMeters <= 0.35f ? "Passed" : "Failed",
                    physics.productionWorld.postStreamingVerticalDeltaMeters, 0.35f, 0f, 0.35f, "m",
                    PhysicsEvidence, "Maximum vertical displacement while supported only by cell_0_-2.");
                AddValue(rows, "physx", "world-transition",
                    "world.streaming_minimum_contact_wheel_count", "RemakeDesignTarget",
                    physics.productionWorld.minimumStreamingContactWheelCount >= 2 ? "Passed" : "Failed",
                    physics.productionWorld.minimumStreamingContactWheelCount, 2f, 2f, 4f, "count",
                    PhysicsEvidence, "Minimum wheel support observed while automatic scene loading was active.");
                AddValue(rows, "physx", "world-transition",
                    "world.streaming_maximum_vertical_delta_m", "RemakeDesignTarget",
                    physics.productionWorld.maximumStreamingVerticalDeltaMeters <= 0.35f
                        ? "Passed"
                        : "Failed",
                    physics.productionWorld.maximumStreamingVerticalDeltaMeters,
                    0.35f, 0f, 0.35f, "m", PhysicsEvidence,
                    "Maximum chassis vertical displacement while automatic scene loading was active.");
                AddValue(rows, "physx", "world-transition",
                    "world.next_cell_only_minimum_contact_wheel_count", "RemakeDesignTarget",
                    physics.productionWorld.nextCellOnlyMinimumContactWheelCount >= 2
                        ? "Passed"
                        : "Failed",
                    physics.productionWorld.nextCellOnlyMinimumContactWheelCount,
                    2f, 2f, 4f, "count", PhysicsEvidence,
                    "cell_0_-3 colliders disabled; support must come from cell_0_-2.");
                AddValue(rows, "physx", "world-transition",
                    "world.next_cell_only_maximum_vertical_delta_m", "RemakeDesignTarget",
                    physics.productionWorld.nextCellOnlyMaximumVerticalDeltaMeters <= 0.35f
                        ? "Passed"
                        : "Failed",
                    physics.productionWorld.nextCellOnlyMaximumVerticalDeltaMeters,
                    0.35f, 0f, 0.35f, "m", PhysicsEvidence,
                    "Maximum vertical displacement with only cell_0_-2 collision enabled.");
                bool surfaceTransitionPassed = physics.productionWorld.sawPavedSurface &&
                                               physics.productionWorld.sawGravelSurface &&
                                               physics.productionWorld.sawNextCellGrassSurface;
                AddValue(rows, "physx", "world-transition",
                    "world.surface_transition_lookup_pass", "RemakeDesignTarget",
                    surfaceTransitionPassed ? "Passed" : "Failed",
                    surfaceTransitionPassed ? 1f : 0f, 1f, 1f, 1f, "bool", PhysicsEvidence,
                    "Wheel contacts resolved garage Paved, driveway/road Gravel, and next-cell " +
                    "ShoreApproach Grass metadata.");
                AddValue(rows, "physx", "garage-clearance", "contact.minimum_wheel_count",
                    "RemakeDesignTarget",
                    physics.productionWorld.minimumContactWheelCount >= 2 ? "Passed" : "Failed",
                    physics.productionWorld.minimumContactWheelCount, 2f, 2f, 4f, "count",
                    PhysicsEvidence, "Minimum contacts while leaving the garage and crossing the boundary.");
            }

            string[] finiteFixtures =
            {
                "start-idle", "acceleration", "braking", "coastdown", "steering-slalom",
                "suspension-bump", "surface-comparison", "hill-start", "garage-clearance",
                "world-transition"
            };
            for (int index = 0; index < finiteFixtures.Length; index++)
            {
                AddValue(rows, "physx", finiteFixtures[index], "numeric.finite_state",
                    "RemakeDesignTarget", "Passed", 1f, 1f, 1f, 1f, "bool",
                    PhysicsEvidence, "Passing suite asserts finite state throughout this fixture.");
            }
        }

        private static void AddCoastdownRows(
            List<string[]> rows,
            CoastdownMetricDto[] trials)
        {
            if (trials == null || trials.Length == 0)
            {
                AddNotRun(rows, "coastdown", "longitudinal.coastdown_speed_loss_m_s",
                    "PhysX coast-down trial array is empty.");
                AddNotRun(rows, "coastdown", "longitudinal.coastdown_max_speed_increase_m_s",
                    "PhysX coast-down trial array is empty.");
                return;
            }

            float[] losses = trials.Select(trial => trial.speedLossMetersPerSecond).ToArray();
            VehicleStatisticalSummary lossSummary = VehicleCalibrationComparator.Summarize(losses);
            bool lossPassed = lossSummary.HasValidSamples && lossSummary.Minimum > 0.02f;
            AddSummary(rows, "physx", "coastdown", "longitudinal.coastdown_speed_loss_m_s",
                "RemakeDesignTarget", lossPassed ? "Passed" : "Failed", lossSummary,
                0.02f, 0.02f, float.PositiveInfinity, "m/s", PhysicsEvidence,
                "Neutral/clutch-disengaged repeated PhysX coast-down trials.");

            float[] increases = trials.Select(trial => Mathf.Max(
                0f,
                trial.maximumSpeedMetersPerSecond - trial.initialSpeedMetersPerSecond)).ToArray();
            VehicleStatisticalSummary increaseSummary =
                VehicleCalibrationComparator.Summarize(increases);
            bool increasePassed = increaseSummary.HasValidSamples && increaseSummary.Maximum <= 0.15f;
            AddSummary(rows, "physx", "coastdown",
                "longitudinal.coastdown_max_speed_increase_m_s", "RemakeDesignTarget",
                increasePassed ? "Passed" : "Failed", increaseSummary,
                0f, 0f, 0.15f, "m/s", PhysicsEvidence,
                "Maximum transient speed increase after propulsion is removed.");
        }

        private static void AddSteeringRows(
            List<string[]> rows,
            SteeringMetricDto steering,
            VehicleSimulationConfig config)
        {
            if (steering == null)
            {
                AddNotRun(rows, "steering-slalom", "lateral.steering_angle_deg",
                    "PhysX steering evidence is empty.");
                AddNotRun(rows, "steering-slalom", "lateral.turning_radius_estimate_m",
                    "PhysX steering evidence is empty.");
                AddNotRun(rows, "steering-slalom", "contact.minimum_wheel_count",
                    "PhysX steering evidence is empty.");
                AddNotRun(rows, "steering-slalom",
                    "contact.maximum_zero_contact_streak_frames",
                    "PhysX steering evidence is empty.");
                return;
            }

            float steeringMagnitude = Mathf.Max(
                steering.maximumPositiveSteeringAngleDegrees,
                Mathf.Abs(steering.minimumNegativeSteeringAngleDegrees));
            bool steeringPassed = steering.maximumPositiveSteeringAngleDegrees > 2f &&
                                  steering.minimumNegativeSteeringAngleDegrees < -2f &&
                                  steeringMagnitude <= config.Dynamics.MaximumSteeringAngleDegrees + 0.1f;
            AddValue(rows, "physx", "steering-slalom", "lateral.steering_angle_deg",
                "RemakeDesignTarget", steeringPassed ? "Passed" : "Failed", steeringMagnitude,
                config.Dynamics.MaximumSteeringAngleDegrees * 0.55f,
                2f, config.Dynamics.MaximumSteeringAngleDegrees + 0.1f, "deg",
                PhysicsEvidence, "Maximum absolute response across positive and negative steering steps.");

            float yawRadians = steering.maximumAbsoluteYawDegrees * Mathf.Deg2Rad;
            float turningRadiusEstimate = yawRadians > 0.001f
                ? steering.forwardProgressMeters / yawRadians
                : float.PositiveInfinity;
            bool radiusAvailable = !float.IsNaN(turningRadiusEstimate) &&
                                   !float.IsInfinity(turningRadiusEstimate) &&
                                   turningRadiusEstimate > 0f;
            if (radiusAvailable)
            {
                AddValue(rows, "physx", "steering-slalom",
                    "lateral.turning_radius_estimate_m", "RemakeDesignTarget", "Informational",
                    turningRadiusEstimate, 4.0429f, 0f, float.PositiveInfinity, "m",
                    PhysicsEvidence,
                    "Coarse path-radius estimate from forward progress / maximum yaw; not donor parity.");
            }
            else
            {
                AddNotRun(rows, "steering-slalom", "lateral.turning_radius_estimate_m",
                    "Steering evidence did not permit a finite path-radius estimate.");
            }

            AddValue(rows, "physx", "steering-slalom", "contact.minimum_wheel_count",
                "RemakeDesignTarget", steering.minimumContactWheelCount >= 2 ? "Passed" : "Failed",
                steering.minimumContactWheelCount, 2f, 2f, 4f, "count", PhysicsEvidence,
                "Minimum wheel contacts during the two steering steps.");
            AddValue(rows, "physx", "steering-slalom",
                "contact.maximum_zero_contact_streak_frames", "RemakeDesignTarget",
                steering.maximumZeroContactStreakFrames <= 5 ? "Passed" : "Failed",
                steering.maximumZeroContactStreakFrames, 0f, 0f, 5f, "frames",
                PhysicsEvidence, "Longest all-wheel airborne streak during steering validation.");
            AddValue(rows, "physx", "steering-slalom", "lateral.maximum_slip",
                "Unknown", "Informational", steering.maximumAbsoluteLateralSlip,
                steering.maximumAbsoluteLateralSlip, 0f, float.PositiveInfinity, "ratio",
                PhysicsEvidence, "Observed maximum absolute lateral slip; donor target Unknown.");
        }

        private static void AddTrialAggregate(
            List<string[]> rows,
            TrialMetricDto[] trials,
            string label,
            Func<TrialMetricDto, float> selector,
            string fixture,
            string metric,
            string unit,
            float minimum,
            float maximum)
        {
            if (trials == null || trials.Length == 0)
            {
                AddNotRun(rows, fixture, metric, "PhysX trial array is empty: " + label);
                return;
            }

            float[] values = trials.Select(selector).ToArray();
            VehicleStatisticalSummary summary = VehicleCalibrationComparator.Summarize(values);
            bool passed = summary.HasValidSamples && !summary.HasInvalidSamples &&
                          summary.Minimum >= minimum && summary.Maximum <= maximum;
            AddSummary(rows, "physx", fixture, metric, "RemakeDesignTarget",
                passed ? "Passed" : "Failed", summary,
                float.IsInfinity(maximum) ? minimum : maximum,
                minimum, maximum, unit, PhysicsEvidence, "Repeated PhysX trials.");
        }

        private static void AddUnknownDynamicRows(List<string[]> rows)
        {
            string[] metrics =
            {
                "mass.donor_total_assembled_kg", "mass.donor_center_of_mass_m",
                "mass.donor_part_contributions_kg", "engine.donor_torque_curve",
                "engine.donor_throttle_response", "engine.donor_free_rev_response",
                "engine.donor_stall_behavior", "engine.donor_engine_braking",
                "clutch.donor_engagement", "clutch.donor_slip",
                "gearbox.donor_ratios", "drivetrain.donor_final_drive",
                "longitudinal.donor_top_speed", "longitudinal.donor_coastdown",
                "longitudinal.donor_braking_distance", "longitudinal.donor_brake_balance",
                "longitudinal.donor_wheel_lock_slip", "longitudinal.hill_start_not_run",
                "lateral.donor_turning_radius", "lateral.donor_understeer_oversteer",
                "lateral.donor_slip_recovery", "suspension.donor_travel_damping_rebound",
                "suspension.body_roll_not_measured", "suspension.pitch_not_measured",
                "world.production_bridge_continuity", "world.production_full_road_route",
                "world.curb_ditch_vehicle_fixture", "world.vehicle_reset_recovery_fixture"
            };
            for (int index = 0; index < metrics.Length; index++)
            {
                AddUnknown(rows, "reference", "reference-only", metrics[index], string.Empty,
                    "No controlled donor/full-production fixture; intentionally not inferred.");
            }
        }

        private static void WriteTuningLog()
        {
            var rows = new List<string[]>
            {
                new[]
                {
                    "change_id", "parameter", "old_value", "new_value", "reason", "target_fixture",
                    "result_before", "result_after", "side_effects", "confidence", "change_type"
                },
                new[]
                {
                    "M06A-001", "builder.default_application_policy", "reset defaults every rebuild",
                    "create-only plus explicit schema migration", "Preserve calibrated values across rebuilds",
                    "all", "Calibration edits could be silently erased", "Rebuild preserves current config",
                    "Legacy configs require one deterministic schema migration", "High", "ArchitectureFix"
                },
                new[]
                {
                    "M06A-002", "dynamics.center_of_mass_m", "implicit collider-derived (0,0.2,0)",
                    "explicit config (0,0.2,0)", "Make CoM recorded and reproducible",
                    "static-geometry-mass", "Not a first-class calibration parameter",
                    "Central profile/config records same effective value", "No numerical behavior change intended",
                    "High", "OwnershipMigration"
                },
                new[]
                {
                    "M06A-003", "dynamics.chassis_collider", "builder literals center=(0,0.2,0); size=(1.35,0.42,3.15)",
                    "central config; identical numbers", "Remove hidden tuning from scene builder",
                    "garage-clearance", "Could drift outside change log", "Validated against config",
                    "No numerical behavior change intended", "High", "OwnershipMigration"
                },
                new[]
                {
                    "M06A-004", "dynamics.rigidbody_damping", "builder literals linear=0.015; angular=0.15",
                    "central config; identical numbers", "Coast-down inputs must be explicit",
                    "coastdown", "Hidden implementation value", "Recorded validation parameter",
                    "No numerical behavior change intended", "High", "OwnershipMigration"
                },
                new[]
                {
                    "M06A-005", "production.home_surface_metadata", "Unknown on home production colliders",
                    "garage=Paved; driveway/road=Gravel; terrain=Grass",
                    "Enable explicit bounded world-surface lookup", "garage-clearance;world-transition",
                    "Vehicle contacts resolved Unknown", "Typed project-owned semantic surfaces",
                    "Does not create donor friction parity or full-world layer policy", "Medium", "ValidationBlockerFix"
                },
                new[]
                {
                    "M06A-006", "numeric_physics_tuning", "M06 provisional values",
                    "unchanged", "No donor dynamic targets justify retuning",
                    "all", "Accepted M06 baseline", "Validated without fabricated donor calibration",
                    "Known deviations remain explicit", "High", "NoNumericChange"
                },
                new[]
                {
                    "M06A-007", "validation.idle_measurement_window",
                    "measure first 2 s immediately after engine reaches Running",
                    "discard 2 s settling transient, then measure the following 2 s",
                    "Separate starter-to-idle convergence from settled idle stability",
                    "start-idle", "Average 756.138 RPM included the starter recovery transient",
                    "Average 902.531 RPM; maximum settled-window deviation 11.268 RPM",
                    "Adds two seconds of explicit warmup per PlayMode trial; physics values unchanged",
                    "High", "ValidationProtocolFix"
                },
                new[]
                {
                    "M06A-008", "validation.controlled_preroll_clutch",
                    "fixed 0.70/0.72 clutch pedal for two seconds; garage high-RPM target 0.68",
                    "RPM/speed-aware bounded clutch ramp; garage high-RPM target 0.42 and 0.01/tick release",
                    "Avoid setup stalls and threshold flakiness without weakening measured criteria",
                    "acceleration;coastdown;steering-slalom;world-transition",
                    "Coastdown 0.300303 m/s; steering 0.227143 m/s; one full-suite world run stopped at the threshold",
                    "Coastdown 1.022530 m/s; steering 1.000652 m/s; world progress 12.270996 m",
                    "Setup duration is speed-bounded instead of fixed; measured coastdown/steering phases remain unchanged",
                    "High", "ValidationProtocolFix"
                }
            };
            WriteCsv("TUNING_CHANGE_LOG.csv", rows);
        }

        private static void AddValue(
            List<string[]> rows,
            string run,
            string fixture,
            string metric,
            string classification,
            string status,
            float value,
            float target,
            float acceptedMin,
            float acceptedMax,
            string unit,
            string source,
            string notes)
        {
            string runId = ResolveRunId(run, fixture);
            string resolvedStatus = status;
            if (string.Equals(status, "Passed", StringComparison.Ordinal) &&
                (value < acceptedMin || value > acceptedMax))
            {
                resolvedStatus = "Failed";
            }

            rows.Add(new[]
            {
                runId + ":" + metric, runId, fixture, metric, classification, resolvedStatus, "1",
                Number(value), Number(value), Number(value), Number(value), "0", Number(target),
                FiniteOrBlank(acceptedMin), FiniteOrBlank(acceptedMax), unit, source, notes
            });
        }

        private static void AddMetricResultRow(
            List<string[]> rows,
            VehicleCalibrationProfile profile,
            VehicleMetricResult result,
            string source,
            string notes)
        {
            profile.TryGetMetricDefinition(result.MetricId, out VehicleMetricDefinition definition);
            rows.Add(new[]
            {
                result.RunId + ":" + result.MetricId,
                result.RunId,
                result.FixtureId,
                result.MetricId,
                result.Classification.ToString(),
                result.Status.ToString(),
                result.Summary.ValidSampleCount.ToString(),
                Number(result.Summary.Mean),
                Number(result.Summary.Median),
                Number(result.Summary.Minimum),
                Number(result.Summary.Maximum),
                Number(result.Summary.StandardDeviation),
                Number(result.TargetValue),
                Number(result.AcceptedMinimum),
                Number(result.AcceptedMaximum),
                definition?.Unit ?? string.Empty,
                source,
                notes + " " + result.Reason
            });
        }

        private static void AddSummary(
            List<string[]> rows,
            string run,
            string fixture,
            string metric,
            string classification,
            string status,
            VehicleStatisticalSummary summary,
            float target,
            float acceptedMin,
            float acceptedMax,
            string unit,
            string source,
            string notes)
        {
            string runId = ResolveRunId(run, fixture);
            string resolvedStatus = status;
            if (summary.HasInvalidSamples)
            {
                resolvedStatus = "InvalidSamples";
            }
            else if (string.Equals(status, "Passed", StringComparison.Ordinal) &&
                     (!summary.HasValidSamples ||
                      summary.Minimum < acceptedMin || summary.Maximum > acceptedMax))
            {
                resolvedStatus = "Failed";
            }

            rows.Add(new[]
            {
                runId + ":" + metric, runId, fixture, metric, classification, resolvedStatus,
                summary.ValidSampleCount.ToString(), Number(summary.Mean), Number(summary.Median),
                Number(summary.Minimum), Number(summary.Maximum), Number(summary.StandardDeviation),
                Number(target), FiniteOrBlank(acceptedMin), FiniteOrBlank(acceptedMax), unit, source, notes
            });
        }

        private static void AddUnknown(
            List<string[]> rows,
            string run,
            string fixture,
            string metric,
            string unit,
            string notes)
        {
            string runId = ResolveRunId(run, fixture);
            rows.Add(new[]
            {
                runId + ":" + metric, runId, fixture, metric, "Unknown", "Unknown", "0",
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, unit, "Docs/ReferenceCapture/MISSING_REFERENCE_DATA.csv", notes
            });
        }

        private static void AddNotRun(
            List<string[]> rows,
            string fixture,
            string metric,
            string notes,
            string classification = "RemakeDesignTarget")
        {
            string runId = ResolveRunId("physx", fixture);
            rows.Add(new[]
            {
                runId + ":" + metric, runId, fixture, metric, classification, "NotRun", "0",
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, PhysicsEvidence, notes
            });
        }

        private static string ResolveRunId(string run, string fixture)
        {
            switch (run)
            {
                case "pure":
                    return "m06a-pure-start-idle";
                case "physx":
                    return "m06a-physx-" + fixture;
                case "reference":
                    return "m06a-reference-reference-only";
                case "static":
                    return fixture == "static-geometry-mass"
                        ? "m06a-static-geometry-mass"
                        : "m06a-static-" + fixture;
                default:
                    return run;
            }
        }

        private static PhysicsRunEvidenceDto ReadPhysicsEvidence(
            VehicleSimulationConfig config,
            VehicleCalibrationProfile profile)
        {
            string path = Path.GetFullPath(PhysicsEvidence);
            if (!File.Exists(path))
            {
                return null;
            }

            PhysicsRunEvidenceDto evidence = JsonUtility.FromJson<PhysicsRunEvidenceDto>(
                File.ReadAllText(path));
            if (evidence == null ||
                evidence.schemaVersion != VehiclePhysicsValidationProtocol.EvidenceSchemaVersion ||
                !evidence.passed ||
                !string.Equals(
                    evidence.validatorId,
                    VehiclePhysicsValidationProtocol.ValidatorId,
                    StringComparison.Ordinal) ||
                !string.Equals(evidence.unityVersion, Application.unityVersion, StringComparison.Ordinal) ||
                !string.Equals(evidence.vehicleConfigurationId, config.ConfigurationId, StringComparison.Ordinal) ||
                evidence.tuningSchemaVersion != config.TuningSchemaVersion ||
                !string.Equals(evidence.profileRevision, profile.Revision, StringComparison.Ordinal) ||
                !string.Equals(
                    evidence.profileFingerprint,
                    profile.ComputeContentFingerprint(),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evidence.configJsonHash,
                    Hash128.Compute(JsonUtility.ToJson(config)).ToString(),
                    StringComparison.Ordinal) ||
                evidence.repeatTrialCount != 3 ||
                Mathf.Abs(evidence.fixedDeltaSeconds - Time.fixedDeltaTime) > 0.000001f ||
                evidence.trials == null || evidence.trials.Length != 3 ||
                evidence.coastdownTrials == null || evidence.coastdownTrials.Length != 3 ||
                evidence.surfaces == null || evidence.surfaces.Length < 4 ||
                evidence.steering == null || evidence.bump == null || evidence.hill == null ||
                evidence.productionWorld == null ||
                !HasValidScriptedPhysxPerformance(evidence.scriptedPhysxPerformance) ||
                !HasValidTelemetryFiles(evidence.telemetryFiles) ||
                !DateTime.TryParse(
                    evidence.capturedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                Debug.LogWarning(
                    "M06A PhysX evidence exists but does not match the active validator/config/profile.");
                return null;
            }

            return evidence;
        }

        private static bool HasValidScriptedPhysxPerformance(
            ScriptedPhysxPerformanceDto performance)
        {
            if (performance == null ||
                !performance.passed ||
                performance.warmupTicksPerTrial < 1 ||
                performance.measuredTicksPerTrial < 1 ||
                performance.trialCount < 1 ||
                performance.substeps < 1 ||
                performance.rows == null ||
                performance.rows.Length < 2)
            {
                return false;
            }

            for (int index = 0; index < performance.rows.Length; index++)
            {
                ScriptedPhysxPerformanceRowDto row = performance.rows[index];
                if (row == null ||
                    !row.passedSanityCheck ||
                    row.measuredTicks !=
                    performance.measuredTicksPerTrial * performance.trialCount ||
                    row.invalidStateCount != 0 ||
                    row.minimumContactWheelCount < 2 ||
                    row.allocatedBytesPerTick > 0.25d ||
                    !IsFinitePositive(row.rootTickMilliseconds?.mean ?? 0d) ||
                    !IsFinitePositive(row.physicsSimulateMilliseconds?.mean ?? 0d) ||
                    !IsFinitePositive(row.combinedMilliseconds?.mean ?? 0d))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFinitePositive(double value) =>
            value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool HasValidTelemetryFiles(string[] relativePaths)
        {
            if (relativePaths == null || relativePaths.Length < 7)
            {
                return false;
            }

            string telemetryRoot = Path.GetFullPath(
                VehiclePhysicsValidationPaths.DocumentationRoot + "/Telemetry")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            for (int index = 0; index < relativePaths.Length; index++)
            {
                string path = relativePaths[index] ?? string.Empty;
                string fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(telemetryRoot, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(fullPath) || new FileInfo(fullPath).Length <= 256)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteCsv(string name, IReadOnlyList<string[]> rows)
        {
            string path = Path.GetFullPath(
                VehiclePhysicsValidationPaths.DocumentationRoot + "/" + name);
            var builder = new StringBuilder();
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                string[] row = rows[rowIndex];
                for (int column = 0; column < row.Length; column++)
                {
                    if (column > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(Escape(row[column]));
                }

                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            string text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return text;
            }

            return '"' + text.Replace("\"", "\"\"") + '"';
        }

        private static string Number(float value) =>
            value.ToString("0.########", CultureInfo.InvariantCulture);

        private static string FiniteOrBlank(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? string.Empty : Number(value);

        private static string Hardware() =>
            $"{SystemInfo.operatingSystem}; {SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}";

        private sealed class AvailablePrerequisites : IVehicleSimulationPrerequisiteSource
        {
            public void Evaluate(
                in VehicleInputState input,
                ref VehicleSimulationPrerequisites result)
            {
                result.Reset();
                if (!input.IgnitionOn)
                {
                    result.Add(VehicleSimulationPrerequisiteFailure.IgnitionOff);
                }
            }
        }

        private sealed class FixedCalibrationBackend : IWheelPhysicsBackend
        {
            private readonly VehicleSimulationConfig config;

            public FixedCalibrationBackend(VehicleSimulationConfig valueConfig)
            {
                config = valueConfig;
            }

            public int WheelCount => config.WheelCount;
            public float VehicleSpeedMetersPerSecond => 0f;

            public void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination)
            {
                float load = config.Dynamics.ProvisionalMassKilograms * 9.81f / WheelCount;
                for (int index = 0; index < WheelCount; index++)
                {
                    destination[index] = new WheelPhysicsSample(
                        true, Vector3.zero, Vector3.up, load, 0f, 0f, 0f,
                        0.35f, VehicleSurfaceType.Paved);
                }
            }

            public void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands)
            {
            }

            public void Reset()
            {
            }
        }

        private sealed class PureStartIdleEvidence
        {
            public PureStartIdleEvidence(
                float[] starts,
                float[] idles,
                float[] idleDeviations,
                VehicleStatisticalSummary startStats,
                VehicleStatisticalSummary idleStats,
                VehicleStatisticalSummary deviationStats,
                VehicleCalibrationRun calibrationRun)
            {
                startSeconds = starts;
                idleRpm = idles;
                idleMaximumDeviationRpm = idleDeviations;
                startSummary = startStats;
                idleSummary = idleStats;
                idleDeviationSummary = deviationStats;
                run = calibrationRun;
            }

            public readonly float[] startSeconds;
            public readonly float[] idleRpm;
            public readonly float[] idleMaximumDeviationRpm;
            public readonly VehicleStatisticalSummary startSummary;
            public readonly VehicleStatisticalSummary idleSummary;
            public readonly VehicleStatisticalSummary idleDeviationSummary;
            public readonly VehicleCalibrationRun run;
        }

        // JsonUtility populates these serialized DTO fields reflectively.
#pragma warning disable CS0649
        [Serializable]
        private sealed class PhysicsRunEvidenceDto
        {
            public int schemaVersion;
            public string validatorId = string.Empty;
            public bool passed;
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string operatingSystem = string.Empty;
            public string processorType = string.Empty;
            public string graphicsDeviceName = string.Empty;
            public float fixedDeltaSeconds;
            public int repeatTrialCount;
            public string vehicleConfigurationId = string.Empty;
            public int tuningSchemaVersion;
            public string configJsonHash = string.Empty;
            public string profileRevision = string.Empty;
            public string profileFingerprint = string.Empty;
            public TrialMetricDto[] trials = Array.Empty<TrialMetricDto>();
            public SurfaceMetricDto[] surfaces = Array.Empty<SurfaceMetricDto>();
            public CoastdownMetricDto[] coastdownTrials = Array.Empty<CoastdownMetricDto>();
            public SteeringMetricDto steering;
            public BumpMetricDto bump;
            public HillMetricDto hill;
            public string[] telemetryFiles = Array.Empty<string>();
            public WorldMetricDto productionWorld;
            public ScriptedPhysxPerformanceDto scriptedPhysxPerformance;
        }

        [Serializable]
        private sealed class ScriptedPhysxPerformanceDto
        {
            public bool passed;
            public int warmupTicksPerTrial;
            public int measuredTicksPerTrial;
            public int trialCount;
            public float fixedDeltaSeconds;
            public int substeps;
            public ScriptedPhysxPerformanceRowDto[] rows =
                Array.Empty<ScriptedPhysxPerformanceRowDto>();
        }

        [Serializable]
        private sealed class ScriptedPhysxPerformanceRowDto
        {
            public int measuredTicks;
            public TimingSummaryDto rootTickMilliseconds;
            public TimingSummaryDto physicsSimulateMilliseconds;
            public TimingSummaryDto combinedMilliseconds;
            public double allocatedBytesPerTick;
            public int minimumContactWheelCount;
            public int invalidStateCount;
            public bool passedSanityCheck;
        }

        [Serializable]
        private sealed class TimingSummaryDto
        {
            public double mean;
        }

        [Serializable]
        private sealed class TrialMetricDto
        {
            public float startSeconds;
            public float idleAverageRpm;
            public float idleMaximumDeviationRpm;
            public float launchPeakSpeedMetersPerSecond;
            public float launchDistanceMeters;
            public float brakingInitialSpeedMetersPerSecond;
            public float brakingFinalSpeedMetersPerSecond;
            public float brakingDistanceMeters;
            public float maximumBrakingSpeedIncreaseMetersPerSecond;
            public int minimumBrakingContactWheelCount;
            public int finalContactWheelCount;
        }

        [Serializable]
        private sealed class SurfaceMetricDto
        {
            public string surface = string.Empty;
            public int contactWheelCount;
            public float frictionMultiplier;
            public float rollingResistanceMultiplier;
            public float coastdownLossMetersPerSecond;
        }

        [Serializable]
        private sealed class CoastdownMetricDto
        {
            public float initialSpeedMetersPerSecond;
            public float maximumSpeedMetersPerSecond;
            public float speedLossMetersPerSecond;
        }

        [Serializable]
        private sealed class SteeringMetricDto
        {
            public float maximumPositiveSteeringAngleDegrees;
            public float minimumNegativeSteeringAngleDegrees;
            public float maximumAbsoluteYawDegrees;
            public float forwardProgressMeters;
            public float maximumAbsoluteLateralSlip;
            public int minimumContactWheelCount;
            public int maximumZeroContactStreakFrames;
        }

        [Serializable]
        private sealed class BumpMetricDto
        {
            public float baselineAverageCompression01;
            public float peakCompression01;
            public float recoveredAverageCompression01;
            public int minimumBumpContactWheelCount;
            public int recoveredContactWheelCount;
        }

        [Serializable]
        private sealed class HillMetricDto
        {
            public float slopeDegrees;
            public float holdDriftMeters;
            public float uphillProgressMeters;
            public int minimumContactWheelCount;
        }

        [Serializable]
        private sealed class WorldMetricDto
        {
            public float horizontalProgressMeters;
            public float maximumLateralDeviationMeters;
            public int minimumContactWheelCount;
            public int maximumZeroContactStreakFrames;
            public int postStreamingContactWheelCount;
            public float postStreamingVerticalDeltaMeters;
            public int minimumStreamingContactWheelCount;
            public float maximumStreamingVerticalDeltaMeters;
            public int nextCellOnlyMinimumContactWheelCount;
            public float nextCellOnlyMaximumVerticalDeltaMeters;
            public float nextCellContactProbeZ;
            public bool nextCellInitiallyLoaded;
            public bool automaticStreamingObserved;
            public bool sawPavedSurface;
            public bool sawGravelSurface;
            public bool sawNextCellGrassSurface;
            public bool nextCellLoaded;
            public bool garageDoorsOpen;
            public bool drivewayGatesOpen;
        }
#pragma warning restore CS0649
    }
}
