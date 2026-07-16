using System;
using System.Collections.Generic;
using MSC.Editor.VehicleSimulation;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleValidation
{
    internal static class VehiclePhysicsValidationProfileBuilder
    {
        public static VehicleCalibrationProfile Build(VehicleSimulationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            VehicleSimulationEditorUtility.EnsureFolder(VehiclePhysicsValidationPaths.Profiles);
            VehicleCalibrationProfile profile =
                AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                    VehiclePhysicsValidationPaths.Profile);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VehicleCalibrationProfile>();
                AssetDatabase.CreateAsset(profile, VehiclePhysicsValidationPaths.Profile);
            }

            VehicleMetricDefinition[] metrics = BuildMetricDefinitions();
            VehicleCalibrationFixture[] fixtures = BuildFixtures(config);
            VehicleReferenceTarget[] targets = BuildReferenceTargets(config);
            profile.Configure(
                config,
                "m06a.satsuma.physics-validation",
                "M06A Satsuma Physics Validation",
                VehiclePhysicsValidationProtocol.ProfileRevision,
                fixtures,
                metrics,
                targets,
                "All dynamic values remain RemakeDesignTarget/ProvisionalProjectTuning. " +
                "Logical part masses (871.7 kg) are recorded but excluded from the 650 kg " +
                "physics proxy. Tire pressure 0 means Unknown/not simulated, not a 0 kPa claim. " +
                "Candidate radius 0.272667 m remains PlausibilityTarget/NeedsReview.");
            if (!profile.Validate(out string failure))
            {
                throw new InvalidOperationException("M06A calibration profile is invalid: " + failure);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static VehicleCalibrationFixture[] BuildFixtures(VehicleSimulationConfig config)
        {
            return new[]
            {
                Fixture(
                    config,
                    "static-geometry-mass",
                    "Static geometry and mass snapshot",
                    VehicleCalibrationFixtureKind.Custom,
                    "profile/config-snapshot",
                    0.1f,
                    1,
                    new[]
                    {
                        "mass.proxy_total_kg", "mass.logical_parts_sum_kg",
                        "mass.center_of_mass_x_m", "mass.center_of_mass_y_m",
                        "mass.center_of_mass_z_m", "geometry.wheelbase_m",
                        "geometry.front_track_m", "geometry.rear_track_m",
                        "geometry.wheel_radius_m", "geometry.ride_height_m",
                        "geometry.ground_clearance_m", "suspension.rest_length_config_m",
                        "suspension.travel_config_m", "geometry.steering_lock_config_deg"
                    },
                    NeutralFrames()),
                Fixture(
                    config,
                    "start-idle",
                    "Start and idle",
                    VehicleCalibrationFixtureKind.StartIdle,
                    "VehicleSimulationPrototype/runtime-paved-flat",
                    6f,
                    3,
                    new[]
                    {
                        "engine.starter_time_s", "engine.idle_rpm",
                        "engine.idle_max_deviation_rpm", "numeric.finite_state"
                    },
                    StartIdleFrames()),
                Fixture(
                    config,
                    "acceleration",
                    "Bounded launch and acceleration",
                    VehicleCalibrationFixtureKind.Acceleration,
                    "VehicleSimulationPrototype/runtime-paved-flat",
                    8f,
                    3,
                    new[]
                    {
                        "longitudinal.launch_peak_speed_m_s",
                        "longitudinal.launch_distance_m", "numeric.finite_state"
                    },
                    DriveFrames(braking: false, steering: 0f)),
                Fixture(
                    config,
                    "braking",
                    "Bounded service braking",
                    VehicleCalibrationFixtureKind.Braking,
                    "VehicleSimulationPrototype/runtime-paved-flat",
                    10f,
                    3,
                    new[]
                    {
                        "longitudinal.braking_final_speed_ratio",
                        "longitudinal.braking_distance_m",
                        "longitudinal.braking_max_speed_increase_m_s",
                        "contact.minimum_wheel_count", "numeric.finite_state"
                    },
                    DriveFrames(braking: true, steering: 0f)),
                Fixture(
                    config,
                    "coastdown",
                    "Coast-down trend",
                    VehicleCalibrationFixtureKind.CoastDown,
                    "VehicleSimulationPrototype/runtime-paved-flat",
                    12f,
                    3,
                    new[]
                    {
                        "longitudinal.coastdown_speed_loss_m_s",
                        "longitudinal.coastdown_max_speed_increase_m_s",
                        "numeric.finite_state"
                    },
                    CoastdownFrames()),
                Fixture(
                    config,
                    "steering-slalom",
                    "Steering step and slalom",
                    VehicleCalibrationFixtureKind.Slalom,
                    "VehicleSimulationPrototype/runtime-paved-steering-step",
                    12f,
                    1,
                    new[]
                    {
                        "lateral.steering_angle_deg", "lateral.turning_radius_estimate_m",
                        "lateral.maximum_slip", "numeric.finite_state"
                    },
                    DriveFrames(braking: false, steering: 0.55f)),
                Fixture(
                    config,
                    "suspension-bump",
                    "Suspension bump and recovery",
                    VehicleCalibrationFixtureKind.SuspensionBump,
                    "VehicleSimulationPrototype/runtime-paved-80mm-bump",
                    8f,
                    1,
                    new[]
                    {
                        "suspension.bump_peak_delta_01",
                        "suspension.recovery_delta_01", "contact.minimum_wheel_count",
                        "numeric.finite_state"
                    },
                    DriveFrames(braking: false, steering: 0f)),
                Fixture(
                    config,
                    "surface-comparison",
                    "Paved gravel dirt grass comparison",
                    VehicleCalibrationFixtureKind.SurfaceComparison,
                    "VehicleSimulationPrototype/runtime-Paved-Gravel-Dirt-Grass-primitives",
                    8f,
                    1,
                    new[]
                    {
                        "surface.lookup_pass", "surface.friction_ordering_pass",
                        "surface.rolling_resistance_ordering_pass",
                        "contact.minimum_wheel_count", "numeric.finite_state"
                    },
                    DriveFrames(braking: false, steering: 0f)),
                Fixture(
                    config,
                    "hill-start",
                    "Six-degree hill hold and launch",
                    VehicleCalibrationFixtureKind.HillStart,
                    "VehicleSimulationPrototype/runtime-paved-slope-6deg",
                    10f,
                    1,
                    new[]
                    {
                        "longitudinal.hill_hold_drift_m",
                        "longitudinal.hill_start_progress_m",
                        "contact.minimum_wheel_count", "numeric.finite_state"
                    },
                    HillStartFrames()),
                Fixture(
                    config,
                    "garage-clearance",
                    "Garage and threshold clearance",
                    VehicleCalibrationFixtureKind.GarageClearance,
                    "Bootstrap+VehicleSimulationPrototype/production-cell_0_-3-garage-driveway",
                    25f,
                    1,
                    new[]
                    {
                        "world.garage_width_margin_m", "world.garage_height_margin_m",
                        "world.threshold_height_m", "contact.minimum_wheel_count",
                        "numeric.finite_state"
                    },
                    GarageFrames()),
                Fixture(
                    config,
                    "world-transition",
                    "Bounded production streaming boundary",
                    VehicleCalibrationFixtureKind.WorldTransition,
                    "Bootstrap+VehicleSimulationPrototype/production-cell_0_-3_to_0_-2/z_-1024",
                    25f,
                    1,
                    new[]
                    {
                        "world.streaming_neighbor_loaded", "world.route_progress_m",
                        "world.maximum_lateral_deviation_m",
                        "world.post_streaming_contact_wheel_count",
                        "world.post_streaming_vertical_delta_m",
                        "world.streaming_minimum_contact_wheel_count",
                        "world.streaming_maximum_vertical_delta_m",
                        "world.next_cell_only_minimum_contact_wheel_count",
                        "world.next_cell_only_maximum_vertical_delta_m",
                        "world.surface_transition_lookup_pass",
                        "contact.maximum_zero_contact_streak_frames",
                        "numeric.finite_state"
                    },
                    GarageFrames())
            };
        }

        private static VehicleCalibrationFixture Fixture(
            VehicleSimulationConfig config,
            string id,
            string name,
            VehicleCalibrationFixtureKind kind,
            string route,
            float durationSeconds,
            int trials,
            string[] metricIds,
            VehicleScriptedInputFrame[] frames)
        {
            return new VehicleCalibrationFixture(
                id,
                name,
                kind,
                VehicleCalibrationFixtureStatus.Ready,
                0.5f,
                durationSeconds,
                trials,
                0.15f,
                BuildEnvironment(config, id, route, frames),
                metricIds,
                string.Empty,
                "Scripted fixture executed by the named PlayMode test method. Serialized frames record " +
                "nominal phases; adaptive stop/speed conditions are authoritative in test code. " +
                "PhysX variance is evaluated with tolerances, not bitwise equality.");
        }

        private static VehicleCalibrationEnvironment BuildEnvironment(
            VehicleSimulationConfig config,
            string fixtureId,
            string route,
            VehicleScriptedInputFrame[] frames)
        {
            VehicleCalibrationPartMass[] parts = BuildLogicalPartMasses();
            var mass = new VehicleCalibrationMassConfiguration(
                config.Dynamics.ProvisionalMassKilograms,
                0f,
                0f,
                0f,
                config.Dynamics.ProvisionalMassKilograms,
                config.Dynamics.CenterOfMassMeters,
                parts);
            var tires = new[]
            {
                Tire(config, "m06.wheel.fl"),
                Tire(config, "m06.wheel.fr"),
                Tire(config, "m06.wheel.rl"),
                Tire(config, "m06.wheel.rr")
            };
            bool mixedSurfaceFixture = fixtureId == "surface-comparison" ||
                                       fixtureId == "garage-clearance" ||
                                       fixtureId == "world-transition";
            VehicleSurfaceType primarySurface = VehicleSurfaceType.Paved;
            VehicleSurfaceResponse paved = config.GetSurfaceResponse(primarySurface);
            var surface = new VehicleCalibrationSurfaceConfiguration(
                primarySurface,
                mixedSurfaceFixture
                    ? "m06a.mixed-explicit-surfaces;primary=Paved;see-route-and-run-evidence"
                    : "m06.surface.paved.provisional",
                paved.FrictionMultiplier,
                paved.RollingResistanceMultiplier);
            var weather = new VehicleCalibrationWeatherConfiguration(
                "m06a.clear-dry-weather-not-implemented",
                0f,
                config.AmbientTemperatureCelsius,
                0f);
            var load = new VehicleCalibrationLoadConfiguration(
                config.InitialFuelLiters,
                config.InitialOilLiters,
                config.InitialCoolantLiters,
                0f,
                0f);
            var input = new VehicleCalibrationInputConfiguration(
                VehicleCalibrationInputSource.Scripted,
                "MSC.Tests.PlayMode.VehicleSimulation.VehiclePhysicsValidationPlayModeTests/" +
                fixtureId + "/adaptive-v1",
                frames);
            var runtime = new VehicleCalibrationRuntimeConfiguration(
                Time.fixedDeltaTime,
                config.SubstepCount,
                Application.unityVersion,
                "Unity 6000.3 built-in PhysX; native patch not exposed by public API",
                VehicleCalibrationExecutionMode.PlayMode,
                SystemInfo.operatingSystem,
                SystemInfo.processorType,
                SystemInfo.graphicsDeviceName,
                0,
                false);
            return new VehicleCalibrationEnvironment(
                config.ConfigurationId,
                "tuning-schema-" + config.TuningSchemaVersion,
                route,
                mass,
                tires,
                surface,
                weather,
                load,
                input,
                runtime);
        }

        private static VehicleCalibrationTireConfiguration Tire(
            VehicleSimulationConfig config,
            string wheelId)
        {
            return new VehicleCalibrationTireConfiguration(
                wheelId,
                "tire_stock_candidate-radius;fitted-identity-unknown",
                config.Dynamics.WheelRadiusMeters,
                0f,
                0f);
        }

        private static VehicleCalibrationPartMass[] BuildLogicalPartMasses()
        {
            string[] ids =
            {
                "m06.chassis", "m06.engine", "m06.starter", "m06.battery",
                "m06.fuel_tank", "m06.clutch", "m06.gearbox", "m06.differential",
                "m06.wheel.fl", "m06.wheel.fr", "m06.wheel.rl", "m06.wheel.rr"
            };
            float[] masses = { 620f, 92f, 4.2f, 11.5f, 28f, 7f, 31f, 18f, 15f, 15f, 15f, 15f };
            var result = new VehicleCalibrationPartMass[ids.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                result[index] = new VehicleCalibrationPartMass(
                    Hash128.Compute("m06.logical.instance." + ids[index]).ToString(),
                    ids[index],
                    true,
                    false,
                    masses[index]);
            }

            return result;
        }

        private static VehicleMetricDefinition[] BuildMetricDefinitions()
        {
            var result = new List<VehicleMetricDefinition>
            {
                Metric("mass.proxy_total_kg", "Physics proxy total mass", "kg", VehicleReferenceClassification.RemakeDesignTarget, 0.01f, 0f, 1, false, 0f),
                Metric("mass.logical_parts_sum_kg", "Logical assembly mass sum (excluded)", "kg", VehicleReferenceClassification.RemakeDesignTarget, 0.01f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("mass.center_of_mass_x_m", "Explicit proxy CoM X", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.001f, 0f, 1, false, 0f),
                Metric("mass.center_of_mass_y_m", "Explicit proxy CoM Y", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.001f, 0f, 1, false, 0f),
                Metric("mass.center_of_mass_z_m", "Explicit proxy CoM Z", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.001f, 0f, 1, false, 0f),
                Metric("geometry.wheelbase_m", "Wheelbase at wheel roots", "m", VehicleReferenceClassification.DerivedReference, 0.002f, 0f, 1, false, 0f),
                Metric("geometry.front_track_m", "Front track at wheel roots", "m", VehicleReferenceClassification.DerivedReference, 0.002f, 0f, 1, false, 0f),
                Metric("geometry.rear_track_m", "Rear track at wheel roots", "m", VehicleReferenceClassification.DerivedReference, 0.002f, 0f, 1, false, 0f),
                Metric("geometry.wheel_radius_m", "Candidate wheel radius", "m", VehicleReferenceClassification.PlausibilityTarget, 0.001f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                BlockedMetric("geometry.ride_height_m", "Donor ride height", "m", "No controlled donor ride-height fixture."),
                BlockedMetric("geometry.ground_clearance_m", "Donor ground clearance", "m", "No validated chassis clearance marker."),
                Metric("suspension.rest_length_config_m", "Configured suspension rest length", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.001f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("suspension.travel_config_m", "Configured suspension travel", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.001f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("geometry.steering_lock_config_deg", "Configured steering lock", "deg", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("engine.starter_time_s", "Starter time", "s", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0.15f, 3, true, 0.15f),
                Metric("engine.idle_rpm", "Settled idle RPM", "rpm", VehicleReferenceClassification.RemakeDesignTarget, 120f, 0.15f, 3, true, 0.06f),
                Metric("engine.idle_max_deviation_rpm", "Maximum idle deviation", "rpm", VehicleReferenceClassification.RemakeDesignTarget, 20f, 0f, 3, false, 0f),
                Metric("numeric.finite_state", "Finite simulation state", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("longitudinal.launch_peak_speed_m_s", "Two-second launch peak speed", "m/s", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0.15f, 3, true, 0.15f),
                Metric("longitudinal.launch_distance_m", "Two-second launch distance", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0.15f, 3, true, 0.15f),
                Metric("longitudinal.braking_final_speed_ratio", "Final/initial braking speed ratio", "ratio", VehicleReferenceClassification.RemakeDesignTarget, 0.02f, 0f, 3, true, 0.15f),
                Metric("longitudinal.braking_distance_m", "Bounded service-braking distance", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.25f, 0.15f, 3, true, 0.2f),
                Metric("longitudinal.braking_max_speed_increase_m_s", "Maximum speed increase while braking", "m/s", VehicleReferenceClassification.RemakeDesignTarget, 0.02f, 0f, 3, false, 0f),
                Metric("longitudinal.coastdown_speed_loss_m_s", "Coast-down speed loss", "m/s", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0.15f, 3, true, 0.2f),
                Metric("longitudinal.coastdown_max_speed_increase_m_s", "Coast-down maximum speed increase", "m/s", VehicleReferenceClassification.RemakeDesignTarget, 0.05f, 0f, 3, false, 0f),
                Metric("longitudinal.hill_hold_drift_m", "Six-degree hill hold drift", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.05f, 0f, 1, false, 0f),
                Metric("longitudinal.hill_start_progress_m", "Six-degree uphill launch progress", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0f, 1, false, 0f),
                Metric("lateral.steering_angle_deg", "Steering angle response", "deg", VehicleReferenceClassification.RemakeDesignTarget, 1f, 0.1f, 1, false, 0f),
                Metric("lateral.turning_radius_estimate_m", "Kinematic turning radius estimate", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.25f, 0.1f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("lateral.maximum_slip", "Maximum lateral slip", "ratio", VehicleReferenceClassification.Unknown, 0.1f, 0.2f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("suspension.bump_peak_delta_01", "Bump compression delta", "normalized", VehicleReferenceClassification.RemakeDesignTarget, 0.03f, 0f, 1, false, 0f),
                Metric("suspension.recovery_delta_01", "Recovered compression delta", "normalized", VehicleReferenceClassification.RemakeDesignTarget, 0.03f, 0f, 1, false, 0f),
                Metric("surface.lookup_pass", "Typed surface lookup", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("surface.friction_ordering_pass", "Surface friction ordering", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("surface.rolling_resistance_ordering_pass", "Rolling resistance ordering", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("contact.minimum_wheel_count", "Minimum contacting wheels", "count", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("world.garage_width_margin_m", "Garage width margin", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.01f, 0f, 1, false, 0f),
                Metric("world.garage_height_margin_m", "Garage height margin", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.01f, 0f, 1, false, 0f),
                Metric("world.threshold_height_m", "Driveway-to-garage threshold", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.01f, 0f, 1, false, 0f, VehicleMetricDefinitionStatus.Informational),
                Metric("world.streaming_neighbor_loaded", "Neighbor streaming cell loaded", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("world.route_progress_m", "Garage route progress", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0f, 1, false, 0f),
                Metric("world.maximum_lateral_deviation_m", "Garage route lateral deviation", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.1f, 0f, 1, false, 0f),
                Metric("world.post_streaming_contact_wheel_count", "Wheel contacts after automatic neighbor load", "count", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("world.post_streaming_vertical_delta_m", "Vertical displacement after automatic neighbor load", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.02f, 0f, 1, false, 0f),
                Metric("world.streaming_minimum_contact_wheel_count", "Minimum contacts during automatic neighbor load", "count", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("world.streaming_maximum_vertical_delta_m", "Vertical displacement during automatic neighbor load", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.02f, 0f, 1, false, 0f),
                Metric("world.next_cell_only_minimum_contact_wheel_count", "Minimum contacts with only next-cell collision", "count", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("world.next_cell_only_maximum_vertical_delta_m", "Vertical displacement with only next-cell collision", "m", VehicleReferenceClassification.RemakeDesignTarget, 0.02f, 0f, 1, false, 0f),
                Metric("world.surface_transition_lookup_pass", "Production Paved-to-Gravel surface lookup", "bool", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f),
                Metric("contact.maximum_zero_contact_streak_frames", "Maximum all-wheel contact loss", "frames", VehicleReferenceClassification.RemakeDesignTarget, 0f, 0f, 1, false, 0f)
            };
            return result.ToArray();
        }

        private static VehicleMetricDefinition Metric(
            string id,
            string name,
            string unit,
            VehicleReferenceClassification classification,
            float absoluteTolerance,
            float relativeTolerance,
            int samples,
            bool repeatability,
            float maximumCoefficientOfVariation,
            VehicleMetricDefinitionStatus status = VehicleMetricDefinitionStatus.Active)
        {
            return new VehicleMetricDefinition(
                id,
                name,
                unit,
                status,
                classification,
                VehicleMetricStatistic.Mean,
                status == VehicleMetricDefinitionStatus.Informational
                    ? VehicleMetricDirection.Informational
                    : VehicleMetricDirection.TwoSided,
                absoluteTolerance,
                relativeTolerance,
                samples,
                repeatability,
                maximumCoefficientOfVariation,
                string.Empty,
                "M06A metric; classification is authoritative.");
        }

        private static VehicleMetricDefinition BlockedMetric(
            string id,
            string name,
            string unit,
            string reason)
        {
            return new VehicleMetricDefinition(
                id,
                name,
                unit,
                VehicleMetricDefinitionStatus.Blocked,
                VehicleReferenceClassification.Unknown,
                VehicleMetricStatistic.Mean,
                VehicleMetricDirection.Informational,
                0f,
                0f,
                1,
                false,
                0f,
                reason,
                reason);
        }

        private static VehicleReferenceTarget[] BuildReferenceTargets(
            VehicleSimulationConfig config)
        {
            var targets = new List<VehicleReferenceTarget>
            {
                ValueTarget("static-geometry-mass", "mass.proxy_total_kg", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.ProvisionalMassKilograms, 0.01f, "M06 config", "Proxy mass; not donor assembled mass."),
                ValueTarget("static-geometry-mass", "mass.logical_parts_sum_kg", VehicleReferenceClassification.RemakeDesignTarget, 871.7f, 0.01f, "M06 logical AssemblyGraph", "Excluded from physics calibration."),
                ValueTarget("static-geometry-mass", "mass.center_of_mass_x_m", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.CenterOfMassMeters.x, 0.001f, "M06A explicit composition config", "Explicit proxy CoM; donor assembled CoM is Unknown."),
                ValueTarget("static-geometry-mass", "mass.center_of_mass_y_m", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.CenterOfMassMeters.y, 0.001f, "M06A explicit composition config", "Same effective collider-derived value as baseline."),
                ValueTarget("static-geometry-mass", "mass.center_of_mass_z_m", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.CenterOfMassMeters.z, 0.001f, "M06A explicit composition config", "Explicit proxy CoM; donor assembled CoM is Unknown."),
                ValueTarget("static-geometry-mass", "geometry.wheelbase_m", VehicleReferenceClassification.DerivedReference, 2.334f, 0.002f, "ReferenceCaptureDatabase:a2a2647de4c9f2e05568e98a27d59380", "Derived from wheel roots."),
                ValueTarget("static-geometry-mass", "geometry.front_track_m", VehicleReferenceClassification.DerivedReference, 1.2600002f, 0.002f, "ReferenceCaptureDatabase:93b15a1420a962441f82ad53e074ed09", "At wheel roots."),
                ValueTarget("static-geometry-mass", "geometry.rear_track_m", VehicleReferenceClassification.DerivedReference, 1.2060003f, 0.002f, "ReferenceCaptureDatabase:98bbe4fc8f0e5702bfbbbf03592d0b28", "At wheel roots."),
                ValueTarget("static-geometry-mass", "geometry.wheel_radius_m", VehicleReferenceClassification.PlausibilityTarget, 0.272667f, 0.001f, "ReferenceCaptureDatabase:f7d49044cec918b66ac64e0099a7781f", "NeedsReview; fitted identity unknown."),
                UnknownTarget("static-geometry-mass", "geometry.ride_height_m", "No controlled donor ride-height fixture."),
                UnknownTarget("static-geometry-mass", "geometry.ground_clearance_m", "No validated chassis clearance marker."),
                ValueTarget("static-geometry-mass", "suspension.rest_length_config_m", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.SuspensionRestLengthMeters, 0.001f, "M06 config", "Configured ray length component; not donor suspension rest position."),
                ValueTarget("static-geometry-mass", "suspension.travel_config_m", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.SuspensionTravelMeters, 0.001f, "M06 config", "Configured travel; donor travel Missing."),
                ValueTarget("static-geometry-mass", "geometry.steering_lock_config_deg", VehicleReferenceClassification.RemakeDesignTarget, config.Dynamics.MaximumSteeringAngleDegrees, 0.1f, "M06 config", "Donor steering lock Missing."),
                RangeTarget("start-idle", "engine.starter_time_s", 0.1f, 1f, "M06A stability target", "Not donor start-time parity."),
                ValueTarget("start-idle", "engine.idle_rpm", VehicleReferenceClassification.RemakeDesignTarget, 900f, 120f, "M06 config", "Donor idle fixture Missing."),
                MaximumTarget("start-idle", "engine.idle_max_deviation_rpm", 120f, "M06A stability target"),
                MinimumTarget("start-idle", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("acceleration", "longitudinal.launch_peak_speed_m_s", 0.5f, "M06 PlayMode regression gate"),
                MinimumTarget("acceleration", "longitudinal.launch_distance_m", 0.4f, "M06 PlayMode regression gate"),
                MinimumTarget("acceleration", "numeric.finite_state", 1f, "M06A invariant"),
                MaximumTarget("braking", "longitudinal.braking_final_speed_ratio", 0.25f, "M06A braking regression target"),
                MaximumTarget("braking", "longitudinal.braking_distance_m", 25f, "M06A bounded braking route"),
                MaximumTarget("braking", "longitudinal.braking_max_speed_increase_m_s", 0.15f, "M06A monotonicity tolerance"),
                MinimumTarget("braking", "contact.minimum_wheel_count", 2f, "M06A contact stability target"),
                MinimumTarget("braking", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("coastdown", "longitudinal.coastdown_speed_loss_m_s", 0.02f, "M06A no-propulsion trend"),
                MaximumTarget("coastdown", "longitudinal.coastdown_max_speed_increase_m_s", 0.15f, "M06A coast-down monotonicity tolerance"),
                MinimumTarget("coastdown", "numeric.finite_state", 1f, "M06A invariant"),
                MaximumTarget("hill-start", "longitudinal.hill_hold_drift_m", 0.25f, "M06A six-degree brake-hold target"),
                MinimumTarget("hill-start", "longitudinal.hill_start_progress_m", 0.5f, "M06A six-degree uphill launch target"),
                MinimumTarget("hill-start", "contact.minimum_wheel_count", 2f, "M06A hill contact stability target"),
                MinimumTarget("hill-start", "numeric.finite_state", 1f, "M06A invariant"),
                RangeTarget("steering-slalom", "lateral.steering_angle_deg", 1f, 30f, "M06 steering config", "Donor steering lock Missing."),
                ValueTarget("steering-slalom", "lateral.turning_radius_estimate_m", VehicleReferenceClassification.RemakeDesignTarget, 4.0429f, 0.25f, "wheelbase/tan(30deg)", "Kinematic estimate only."),
                UnknownTarget("steering-slalom", "lateral.maximum_slip", "No donor lateral-slip target; observed project value is informational."),
                MinimumTarget("steering-slalom", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("suspension-bump", "suspension.bump_peak_delta_01", 0.1f, "M06A 80mm bump target"),
                MaximumTarget("suspension-bump", "suspension.recovery_delta_01", 0.15f, "M06A recovery target"),
                MinimumTarget("suspension-bump", "contact.minimum_wheel_count", 2f, "M06A contact stability target"),
                MinimumTarget("suspension-bump", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("surface-comparison", "surface.lookup_pass", 1f, "M06A typed metadata contract"),
                MinimumTarget("surface-comparison", "surface.friction_ordering_pass", 1f, "M06 config ordering"),
                MinimumTarget("surface-comparison", "surface.rolling_resistance_ordering_pass", 1f, "M06 config ordering"),
                MinimumTarget("surface-comparison", "contact.minimum_wheel_count", 2f, "M06A contact stability target"),
                MinimumTarget("surface-comparison", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("garage-clearance", "world.garage_width_margin_m", 0.92f, "05A representative envelope fixture"),
                MinimumTarget("garage-clearance", "world.garage_height_margin_m", 0.47f, "05A representative envelope fixture"),
                ValueTarget("garage-clearance", "world.threshold_height_m", VehicleReferenceClassification.RemakeDesignTarget, 0.105f, 0.01f, "Production cell project measurement", "Not donor threshold parity."),
                MinimumTarget("garage-clearance", "contact.minimum_wheel_count", 2f, "M06A bounded route target"),
                MinimumTarget("garage-clearance", "numeric.finite_state", 1f, "M06A invariant"),
                MinimumTarget("world-transition", "world.streaming_neighbor_loaded", 1f, "Production streaming manifest"),
                MinimumTarget("world-transition", "world.route_progress_m", 12.23f, "Garage start to z=-1024 boundary"),
                MaximumTarget("world-transition", "world.maximum_lateral_deviation_m", 1.1f, "Bounded garage corridor"),
                MinimumTarget("world-transition", "world.post_streaming_contact_wheel_count", 2f, "M06A automatic streaming continuity"),
                MaximumTarget("world-transition", "world.post_streaming_vertical_delta_m", 0.35f, "M06A automatic streaming continuity"),
                MinimumTarget("world-transition", "world.streaming_minimum_contact_wheel_count", 2f, "M06A active-load continuity"),
                MaximumTarget("world-transition", "world.streaming_maximum_vertical_delta_m", 0.35f, "M06A active-load continuity"),
                MinimumTarget("world-transition", "world.next_cell_only_minimum_contact_wheel_count", 2f, "M06A next-cell collision proof"),
                MaximumTarget("world-transition", "world.next_cell_only_maximum_vertical_delta_m", 0.35f, "M06A next-cell collision proof"),
                MinimumTarget("world-transition", "world.surface_transition_lookup_pass", 1f, "M06A production semantic-surface lookup"),
                MaximumTarget("world-transition", "contact.maximum_zero_contact_streak_frames", 8f, "Bounded threshold tolerance"),
                MinimumTarget("world-transition", "numeric.finite_state", 1f, "M06A invariant")
            };
            return targets.ToArray();
        }

        private static VehicleReferenceTarget ValueTarget(
            string fixture,
            string metric,
            VehicleReferenceClassification classification,
            float value,
            float tolerance,
            string source,
            string notes)
        {
            return new VehicleReferenceTarget(
                fixture, metric, classification, VehicleReferenceTargetStatus.Provisional,
                VehicleReferenceTargetMode.ValueWithTolerance, value, value, value, true,
                tolerance, 0f, classification == VehicleReferenceClassification.DerivedReference ? 0.95f : 0.65f,
                source, notes);
        }

        private static VehicleReferenceTarget RangeTarget(
            string fixture,
            string metric,
            float minimum,
            float maximum,
            string source,
            string notes)
        {
            return new VehicleReferenceTarget(
                fixture, metric, VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional, VehicleReferenceTargetMode.InclusiveRange,
                0f, minimum, maximum, false, 0f, 0f, 0.6f, source, notes);
        }

        private static VehicleReferenceTarget MinimumTarget(
            string fixture,
            string metric,
            float minimum,
            string source)
        {
            return new VehicleReferenceTarget(
                fixture, metric, VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional, VehicleReferenceTargetMode.Minimum,
                minimum, minimum, 0f, false, 0f, 0f, 0.7f, source,
                "Project-owned regression/stability target; not donor parity.");
        }

        private static VehicleReferenceTarget MaximumTarget(
            string fixture,
            string metric,
            float maximum,
            string source)
        {
            return new VehicleReferenceTarget(
                fixture, metric, VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional, VehicleReferenceTargetMode.Maximum,
                maximum, 0f, maximum, false, 0f, 0f, 0.7f, source,
                "Project-owned regression/stability target; not donor parity.");
        }

        private static VehicleReferenceTarget UnknownTarget(
            string fixture,
            string metric,
            string reason)
        {
            return new VehicleReferenceTarget(
                fixture, metric, VehicleReferenceClassification.Unknown,
                VehicleReferenceTargetStatus.Unknown, VehicleReferenceTargetMode.Unspecified,
                0f, 0f, 0f, false, 0f, 0f, 0f, string.Empty, reason);
        }

        private static VehicleScriptedInputFrame[] NeutralFrames()
        {
            return new[] { Frame(0f, 0f, 1f, 0f, 0f, false, false, false, 0) };
        }

        private static VehicleScriptedInputFrame[] StartIdleFrames()
        {
            return new[]
            {
                Frame(0f, 0f, 1f, 0f, 0f, true, true, false, 0),
                Frame(1f, 0f, 1f, 0f, 0f, true, false, false, 0),
                Frame(6f, 0f, 1f, 0f, 0f, true, false, false, 0)
            };
        }

        private static VehicleScriptedInputFrame[] DriveFrames(bool braking, float steering)
        {
            var frames = new List<VehicleScriptedInputFrame>
            {
                Frame(0f, 0f, 1f, 0f, 0f, true, true, false, 0),
                Frame(1f, 0f, 1f, 0f, 0f, true, false, true, 1),
                Frame(2f, 0.85f, 0.7f, 0f, steering, true, false, false, 1)
            };
            if (braking)
            {
                frames.Add(Frame(6f, 0f, 1f, 1f, 0f, true, false, false, 1));
                frames.Add(Frame(10f, 0f, 1f, 1f, 0f, true, false, false, 1));
            }
            else
            {
                frames.Add(Frame(8f, 0f, 1f, 0f, -steering, true, false, false, 1));
            }

            return frames.ToArray();
        }

        private static VehicleScriptedInputFrame[] CoastdownFrames()
        {
            return new[]
            {
                Frame(0f, 0f, 1f, 0f, 0f, true, true, false, 0),
                Frame(1f, 0f, 1f, 0f, 0f, true, false, true, 1),
                Frame(2f, 0.8f, 0.7f, 0f, 0f, true, false, false, 1),
                Frame(6f, 0f, 1f, 0f, 0f, true, false, true, 0),
                Frame(12f, 0f, 1f, 0f, 0f, true, false, false, 0)
            };
        }

        private static VehicleScriptedInputFrame[] GarageFrames()
        {
            return new[]
            {
                Frame(0f, 0f, 1f, 0f, 0f, true, true, false, 0),
                Frame(1f, 0f, 1f, 0f, 0f, true, false, true, 1),
                Frame(2f, 0.55f, 0.72f, 0f, 0f, true, false, false, 1),
                Frame(25f, 0f, 1f, 1f, 0f, true, false, false, 1)
            };
        }

        private static VehicleScriptedInputFrame[] HillStartFrames()
        {
            return new[]
            {
                Frame(0f, 0f, 1f, 1f, 0f, true, true, false, 0),
                Frame(1f, 0f, 1f, 1f, 0f, true, false, true, 1),
                Frame(3f, 0.7f, 0.72f, 0f, 0f, true, false, false, 1),
                Frame(10f, 0f, 1f, 1f, 0f, true, false, false, 1)
            };
        }

        private static VehicleScriptedInputFrame Frame(
            float time,
            float throttle,
            float clutch,
            float brake,
            float steering,
            bool ignition,
            bool starter,
            bool gearChange,
            int gear)
        {
            return new VehicleScriptedInputFrame(
                time, throttle, clutch, brake, steering, ignition, starter, gearChange, gear);
        }
    }
}
