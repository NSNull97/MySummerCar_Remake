using System;
using System.Linq;
using MSC.Editor.VehicleValidation;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class VehiclePhysicsValidationEditModeTests
    {
        private VehicleSimulationConfig config;
        private VehicleCalibrationProfile profile;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                "Assets/Game/Vehicle/Content/Simulation/Configurations/M06_VehicleSimulationConfig.asset");
            profile = AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                VehiclePhysicsValidationPaths.Profile);
            Assert.That(config, Is.Not.Null, "M06 config is missing; run the M06A builder.");
            Assert.That(profile, Is.Not.Null, "M06A profile is missing; run the M06A builder.");
        }

        [Test]
        public void CalibrationProfile_EditorJsonRoundTripPreservesValidContracts()
        {
            Assert.That(profile.Validate(out string failure), Is.True, failure);
            string json = EditorJsonUtility.ToJson(profile, prettyPrint: true);
            VehicleCalibrationProfile clone = ScriptableObject.CreateInstance<VehicleCalibrationProfile>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(json, clone);
                Assert.That(clone.Validate(out failure), Is.True, failure);
                Assert.That(clone.ProfileId, Is.EqualTo(profile.ProfileId));
                Assert.That(clone.Fixtures.Length, Is.EqualTo(11));
                Assert.That(clone.VehicleConfiguration, Is.SameAs(config));
                Assert.That(clone.ComputeContentFingerprint(), Is.EqualTo(profile.ComputeContentFingerprint()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void Comparator_HandlesRepeatedToleranceUnknownTargetsAndInvalidSamples()
        {
            var definition = new VehicleMetricDefinition(
                "test.metric", "Test metric", "unit", VehicleMetricDefinitionStatus.Active,
                VehicleReferenceClassification.RemakeDesignTarget, VehicleMetricStatistic.Mean,
                VehicleMetricDirection.TwoSided, 0.2f, 0f, 3, true, 0.1f,
                string.Empty, "test");
            var target = new VehicleReferenceTarget(
                "test.fixture", "test.metric", VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional,
                VehicleReferenceTargetMode.ValueWithTolerance, 10f, 10f, 10f, true,
                0.2f, 0f, 0.8f, "test", "test");
            VehicleMetricResult pass = VehicleCalibrationComparator.Compare(
                "test.run", definition, target, new[] { 9.9f, 10f, 10.1f }, 0.1f);
            Assert.That(pass.Status, Is.EqualTo(VehicleMetricResultStatus.Passed));
            Assert.That(pass.Summary.ValidSampleCount, Is.EqualTo(3));
            Assert.That(pass.RepeatabilityPassed, Is.True);

            VehicleMetricResult invalid = VehicleCalibrationComparator.Compare(
                "test.run", definition, target, new[] { float.NaN, float.PositiveInfinity }, 0.1f);
            Assert.That(invalid.Status, Is.EqualTo(VehicleMetricResultStatus.InvalidSamples));

            var unknown = new VehicleReferenceTarget(
                "test.fixture", "test.metric", VehicleReferenceClassification.Unknown,
                VehicleReferenceTargetStatus.Unknown, VehicleReferenceTargetMode.Unspecified,
                0f, 0f, 0f, false, 0f, 0f, 0f, string.Empty, "Missing target.");
            VehicleMetricResult unavailable = VehicleCalibrationComparator.Compare(
                "test.run", definition, unknown, new[] { 10f, 10f, 10f }, 0.1f);
            Assert.That(unavailable.Status, Is.EqualTo(VehicleMetricResultStatus.UnknownReference));

            VehicleMetricResult invalidUnknown = VehicleCalibrationComparator.Compare(
                "test.run", definition, unknown, new[] { float.NaN }, 0.1f);
            Assert.That(invalidUnknown.Status, Is.EqualTo(VehicleMetricResultStatus.InvalidSamples));

            var informationalDefinition = new VehicleMetricDefinition(
                "test.info", "Informational metric", "unit",
                VehicleMetricDefinitionStatus.Informational,
                VehicleReferenceClassification.RemakeDesignTarget,
                VehicleMetricStatistic.Mean, VehicleMetricDirection.Informational,
                0f, 0f, 3, true, 0.0001f, string.Empty, "test");
            var informationalTarget = new VehicleReferenceTarget(
                "test.fixture", "test.info", VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional, VehicleReferenceTargetMode.Informational,
                0f, 0f, 0f, false, 0f, 0f, 0.5f, "test", "test");
            VehicleMetricResult informational = VehicleCalibrationComparator.Compare(
                "test.run", informationalDefinition, informationalTarget,
                new[] { 1f, 100f, 1000f }, 0.0001f);
            Assert.That(informational.Status, Is.EqualTo(VehicleMetricResultStatus.Informational));

            var mismatchedTarget = new VehicleReferenceTarget(
                "test.fixture", "test.other", VehicleReferenceClassification.RemakeDesignTarget,
                VehicleReferenceTargetStatus.Provisional,
                VehicleReferenceTargetMode.ValueWithTolerance, 10f, 10f, 10f, true,
                0.2f, 0f, 0.8f, "test", "test");
            VehicleMetricResult mismatch = VehicleCalibrationComparator.Compare(
                "test.run", definition, mismatchedTarget, new[] { 10f, 10f, 10f }, 0.1f);
            Assert.That(mismatch.Status, Is.EqualTo(VehicleMetricResultStatus.InvalidConfiguration));
        }

        [Test]
        public void CalibrationRun_FailedTrialCannotResolveAsPassedWithoutMetricRows()
        {
            Assert.That(profile.TryGetFixture("start-idle", out VehicleCalibrationFixture fixture), Is.True);
            var run = new VehicleCalibrationRun(
                "test.failed-run", profile.ProfileId, fixture.FixtureId,
                DateTime.UtcNow.ToString("O"), VehicleCalibrationRunStatus.Running,
                fixture.Environment);
            var failedTrial = new VehicleCalibrationTrial(
                0, 0, VehicleCalibrationTrialStatus.InvalidNumericState, 0.5f,
                Array.Empty<VehicleMetricTrialObservation>(), "synthetic invalid state");
            run.Complete(
                DateTime.UtcNow.ToString("O"),
                new[] { failedTrial },
                Array.Empty<VehicleMetricResult>());

            Assert.That(run.Status, Is.EqualTo(VehicleCalibrationRunStatus.Failed));
            Assert.That(run.Validate(out string failure), Is.True, failure);
        }

        [Test]
        public void Config_RpmSpeedMassComSuspensionAndSurfaceRelationsRemainFinite()
        {
            Assert.That(config.Validate(out string failure), Is.True, failure);
            Vector3 half = config.Dynamics.ChassisColliderSizeMeters * 0.5f;
            Vector3 relative = config.Dynamics.CenterOfMassMeters -
                               config.Dynamics.ChassisColliderCenterMeters;
            Assert.That(Mathf.Abs(relative.x), Is.LessThanOrEqualTo(half.x));
            Assert.That(Mathf.Abs(relative.y), Is.LessThanOrEqualTo(half.y));
            Assert.That(Mathf.Abs(relative.z), Is.LessThanOrEqualTo(half.z));

            const float speedMetersPerSecond = 10f;
            float previousRpm = float.PositiveInfinity;
            for (int gear = 1; gear <= config.Gearbox.ForwardGearCount; gear++)
            {
                Assert.That(config.Gearbox.TryGetRatio(gear, out float ratio), Is.True);
                float rpm = speedMetersPerSecond / config.Dynamics.WheelRadiusMeters *
                            ratio * config.Gearbox.FinalDriveRatio *
                            VehicleSimulationMath.RadiansPerSecondToRpm;
                Assert.That(VehicleSimulationMath.IsFinite(rpm), Is.True);
                Assert.That(rpm, Is.GreaterThan(0f));
                Assert.That(rpm, Is.LessThan(previousRpm));
                previousRpm = rpm;
            }

            var suspension = new SuspensionSimulation();
            float staticCompression = config.Dynamics.ProvisionalMassKilograms * 9.81f /
                                      config.WheelCount /
                                      config.Dynamics.SpringRateNewtonPerMeter;
            float staticForce = suspension.CalculateForceNewtons(
                config.Dynamics, staticCompression, 0f);
            float compressedForce = suspension.CalculateForceNewtons(
                config.Dynamics, staticCompression + 0.05f, 0.5f);
            Assert.That(staticForce, Is.EqualTo(
                config.Dynamics.ProvisionalMassKilograms * 9.81f / config.WheelCount).Within(0.1f));
            Assert.That(compressedForce, Is.GreaterThan(staticForce));
            Assert.That(VehicleSimulationMath.IsFinite(compressedForce), Is.True);

            VehicleSurfaceType[] types =
            {
                VehicleSurfaceType.Paved, VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt, VehicleSurfaceType.Grass, VehicleSurfaceType.MudWet
            };
            for (int index = 1; index < types.Length; index++)
            {
                VehicleSurfaceResponse previous = config.GetSurfaceResponse(types[index - 1]);
                VehicleSurfaceResponse current = config.GetSurfaceResponse(types[index]);
                Assert.That(previous.FrictionMultiplier, Is.GreaterThan(current.FrictionMultiplier));
                Assert.That(previous.RollingResistanceMultiplier, Is.LessThan(current.RollingResistanceMultiplier));
            }
        }

        [Test]
        public void ValidationScene_HasTypedCompleteRouteAndGarageFixture()
        {
            Scene scene = EditorSceneManager.OpenScene(
                VehiclePhysicsValidationPaths.Scene,
                OpenSceneMode.Additive);
            try
            {
                VehiclePhysicsValidationRig rig = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VehiclePhysicsValidationRig>(true))
                    .Single();
                Assert.That(rig.Validate(out string failure), Is.True, failure);
                Assert.That(rig.Route.Sections.Count, Is.EqualTo(8));
                Assert.That(
                    rig.Route.TryGetSection(
                        "garage-clearance-50m",
                        out VehicleValidationRouteSection garage),
                    Is.True);
                Assert.That(garage.GarageOpeningWidthMeters, Is.EqualTo(3.12f).Within(0.001f));
                Assert.That(garage.GarageOpeningHeightMeters, Is.EqualTo(2.22f).Within(0.001f));
                Assert.That(garage.MaximumCollisionStepMeters, Is.EqualTo(0.105f).Within(0.001f));
                Assert.That(
                    rig.Route.TryGetSection(
                        "coplanar-collider-transition",
                        out VehicleValidationRouteSection transition),
                    Is.True);
                Assert.That(transition.MaximumCollisionStepMeters, Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void ProductionHomePrefabs_ExposeExplicitVehicleSurfaceMetadata()
        {
            GameObject terrainRoad = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/World/Production/Prefabs/WR_HomeTerrainRoadDitch.prefab");
            GameObject garage = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/World/Production/Prefabs/WR_HomeGarage.prefab");
            Assert.That(terrainRoad, Is.Not.Null);
            Assert.That(garage, Is.Not.Null);

            VehicleSurfaceMetadataAuthoring[] worldSurfaces =
                terrainRoad.GetComponentsInChildren<VehicleSurfaceMetadataAuthoring>(true);
            Assert.That(worldSurfaces.Length, Is.EqualTo(3));
            Assert.That(worldSurfaces.Single(item => item.name == "Road").SurfaceType,
                Is.EqualTo(VehicleSurfaceType.Gravel));
            Assert.That(worldSurfaces.Single(item => item.name == "Driveway").SurfaceType,
                Is.EqualTo(VehicleSurfaceType.Gravel));
            Assert.That(worldSurfaces.Single(item => item.name == "Terrain").SurfaceType,
                Is.EqualTo(VehicleSurfaceType.Grass));
            Assert.That(
                garage.GetComponentsInChildren<VehicleSurfaceMetadataAuthoring>(true)
                    .Single(item => item.name == "Floor").SurfaceType,
                Is.EqualTo(VehicleSurfaceType.Paved));
        }

        [Test]
        public void ProductionStreamingManifest_ContainsContiguousBoundedCells()
        {
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    "Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset");
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.ValidateConfiguration(), Is.Empty);
            Assert.That(manifest.CellSizeMeters, Is.EqualTo(512f));
            Assert.That(manifest.TryGetCell("cell_0_-3", out ProductionWorldCellScene pilot), Is.True);
            Assert.That(manifest.TryGetCell("cell_0_-2", out ProductionWorldCellScene next), Is.True);
            Assert.That(next.Index.X, Is.EqualTo(pilot.Index.X));
            Assert.That(next.Index.Z - pilot.Index.Z, Is.EqualTo(1));
            Assert.That(pilot.BuildIndex, Is.EqualTo(6));
            Assert.That(next.BuildIndex, Is.EqualTo(8));
        }
    }
}
