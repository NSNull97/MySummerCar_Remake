using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using MSC.World.Remaster;
using MSC.World.Streaming;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.Tests.PlayMode.VehicleSimulation
{
    /// <summary>
    /// PhysX-backed M06A validation. The suite keeps the accepted M06 prototype
    /// scene immutable and creates all additional fixtures only at runtime.
    /// </summary>
    public sealed class VehiclePhysicsValidationPlayModeTests
    {
        private const int FixedFramesPerSecond = 50;
        private const int TrialCount = 3;
        private const float BrakeSpeedIncreaseToleranceMetersPerSecond = 0.15f;
        private const string EvidenceRelativePath =
            "Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json";
        private static readonly Vector3 ProductionGarageStart =
            new Vector3(153.495f, 1.665734f, -1036.23f);
        private const float ProductionCellBoundaryZ = -1024f;
        private const float NextCellContactProbeZ = -970f;

        private static EvidenceAccumulator evidence = new EvidenceAccumulator();
        private static double performanceChecksum;

        private GameObject runtimeFixtureRoot;
        private VehicleTelemetryRecorder telemetryRecorder;
        private string telemetryRelativePath = string.Empty;
        private readonly List<Collider> disabledPilotCellColliders = new List<Collider>();

        [OneTimeSetUp]
        public void ResetEvidenceBeforeSuite()
        {
            evidence = new EvidenceAccumulator();
            performanceChecksum = 0d;
            string path = GetEvidenceAbsolutePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string temporary = path + ".tmp";
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        [OneTimeTearDown]
        public void WriteEvidenceAfterFullyPassingSuite()
        {
            bool complete = evidence.repeatTrialsPassed &&
                            evidence.surfaceContactsPassed &&
                            evidence.coastdownPassed &&
                            evidence.steeringStepPassed &&
                            evidence.bumpRecoveryPassed &&
                            evidence.hillStartPassed &&
                            evidence.productionWorldPassed &&
                            evidence.scriptedPhysxPerformancePassed &&
                            evidence.telemetryFiles.Count >= 7;
            if (complete &&
                TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Passed)
            {
                WritePassingEvidence();
            }
        }

        [UnityTest, Order(1)]
        public IEnumerator StartIdleLaunchAndBraking_ThreeTrialsRemainFiniteAndRepeatable()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "start-idle-acceleration-braking");
            var trials = new TrialMetricDto[TrialCount];

            for (int trialIndex = 0; trialIndex < TrialCount; trialIndex++)
            {
                TrialMetricDto metric = new TrialMetricDto { trialIndex = trialIndex + 1 };
                trials[trialIndex] = metric;
                PreparePrototypeForTrial(context);
                yield return WaitFixedFrames(4);

                context.Input.SetContinuousControls(
                    throttle: 0f,
                    clutchPedal: 1f,
                    brake: 0f,
                    steeringInput: 0f,
                    ignition: true,
                    starter: true);
                for (int frame = 1; frame <= FixedFramesPerSecond * 3; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "start", frame);
                    if (context.Host.State.EngineStatus == VehicleEngineStatus.Running)
                    {
                        metric.startFrames = frame;
                        break;
                    }
                }

                Assert.That(
                    context.Host.State.EngineStatus,
                    Is.EqualTo(VehicleEngineStatus.Running),
                    $"Trial {metric.trialIndex} did not start in three seconds.");
                Assert.That(metric.startFrames, Is.GreaterThan(0));
                metric.startSeconds = metric.startFrames * Time.fixedDeltaTime;

                context.Input.SetContinuousControls(
                    throttle: 0f,
                    clutchPedal: 1f,
                    brake: 0f,
                    steeringInput: 0f,
                    ignition: true,
                    starter: false);
                yield return WaitFixedFrames(FixedFramesPerSecond * 2);
                float idleRpmSum = 0f;
                float maximumIdleDeviation = 0f;
                const int idleMeasurementFrames = FixedFramesPerSecond * 2;
                for (int frame = 0; frame < idleMeasurementFrames; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "idle", frame);
                    float rpm = context.Host.State.EngineRpm;
                    idleRpmSum += rpm;
                    maximumIdleDeviation = Mathf.Max(
                        maximumIdleDeviation,
                        Mathf.Abs(rpm - context.Host.Config.Engine.IdleTargetRpm));
                }

                metric.idleAverageRpm = idleRpmSum / idleMeasurementFrames;
                metric.idleMaximumDeviationRpm = maximumIdleDeviation;
                Assert.That(
                    metric.idleAverageRpm,
                    Is.EqualTo(context.Host.Config.Engine.IdleTargetRpm).Within(120f));

                context.Input.RequestGear(1);
                context.Input.SetContinuousControls(
                    throttle: 0.2f,
                    clutchPedal: 1f,
                    brake: 0f,
                    steeringInput: 0f,
                    ignition: true,
                    starter: false);
                yield return new WaitForFixedUpdate();
                Assert.That(context.Host.State.SelectedGear, Is.EqualTo(1));

                Vector3 launchStart = context.Backend.Chassis.position;
                yield return AccelerateStraightToMinimumSpeed(
                    context,
                    "launch",
                    minimumSpeedMetersPerSecond: 1f,
                    maximumFrames: FixedFramesPerSecond * 6);
                metric.launchPeakSpeedMetersPerSecond =
                    HorizontalSpeed(context.Backend.Chassis);

                metric.launchDistanceMeters = HorizontalDistance(
                    launchStart,
                    context.Backend.Chassis.position);
                metric.brakingInitialSpeedMetersPerSecond =
                    HorizontalSpeed(context.Backend.Chassis);
                Vector3 brakingStart = context.Backend.Chassis.position;
                Assert.That(metric.launchPeakSpeedMetersPerSecond, Is.GreaterThan(0.5f));
                Assert.That(metric.launchDistanceMeters, Is.GreaterThan(0.4f));
                Assert.That(metric.brakingInitialSpeedMetersPerSecond, Is.GreaterThan(0.5f));

                context.Input.SetContinuousControls(
                    throttle: 0f,
                    clutchPedal: 1f,
                    brake: 1f,
                    steeringInput: 0f,
                    ignition: true,
                    starter: false);
                float previousSpeed = metric.brakingInitialSpeedMetersPerSecond;
                float finalSpeed = previousSpeed;
                metric.minimumBrakingContactWheelCount = context.Host.Config.WheelCount;
                for (int frame = 1; frame <= FixedFramesPerSecond * 3; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "braking", frame);
                    finalSpeed = HorizontalSpeed(context.Backend.Chassis);
                    float increase = finalSpeed - previousSpeed;
                    metric.maximumBrakingSpeedIncreaseMetersPerSecond = Mathf.Max(
                        metric.maximumBrakingSpeedIncreaseMetersPerSecond,
                        increase);
                    Assert.That(
                        finalSpeed,
                        Is.LessThanOrEqualTo(
                            previousSpeed + BrakeSpeedIncreaseToleranceMetersPerSecond),
                        $"Trial {metric.trialIndex} braking speed rose by {increase:0.######} m/s " +
                        $"at frame {frame}; previous={previousSpeed:0.######}, " +
                        $"current={finalSpeed:0.######}.");
                    previousSpeed = finalSpeed;
                    metric.minimumBrakingContactWheelCount = Mathf.Min(
                        metric.minimumBrakingContactWheelCount,
                        CountContactWheels(context.Host));
                    metric.brakingFrames = frame;
                    if (finalSpeed <= 0.2f)
                    {
                        break;
                    }
                }

                metric.brakingFinalSpeedMetersPerSecond = finalSpeed;
                metric.brakingDistanceMeters = HorizontalDistance(
                    brakingStart,
                    context.Backend.Chassis.position);
                metric.brakingSeconds = metric.brakingFrames * Time.fixedDeltaTime;
                metric.finalContactWheelCount = CountContactWheels(context.Host);
                Assert.That(
                    metric.brakingFinalSpeedMetersPerSecond,
                    Is.LessThanOrEqualTo(
                        Mathf.Max(0.25f, metric.brakingInitialSpeedMetersPerSecond * 0.25f)));
                Assert.That(metric.finalContactWheelCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(metric.minimumBrakingContactWheelCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(metric.brakingDistanceMeters, Is.GreaterThan(0f).And.LessThanOrEqualTo(25f));

                Debug.Log(
                    "M06A_PHYSX_METRICS scenario=start-idle-launch-braking " +
                    $"trial={metric.trialIndex} startFrames={metric.startFrames} " +
                    $"startSeconds={Format(metric.startSeconds)} " +
                    $"idleAverageRpm={Format(metric.idleAverageRpm)} " +
                    $"idleMaximumDeviationRpm={Format(metric.idleMaximumDeviationRpm)} " +
                    $"launchPeakSpeedMetersPerSecond={Format(metric.launchPeakSpeedMetersPerSecond)} " +
                    $"launchDistanceMeters={Format(metric.launchDistanceMeters)} " +
                    $"brakingInitialSpeedMetersPerSecond={Format(metric.brakingInitialSpeedMetersPerSecond)} " +
                    $"brakingFinalSpeedMetersPerSecond={Format(metric.brakingFinalSpeedMetersPerSecond)} " +
                    $"brakingDistanceMeters={Format(metric.brakingDistanceMeters)} " +
                    $"brakingFrames={metric.brakingFrames} " +
                    $"maximumBrakingSpeedIncreaseMetersPerSecond=" +
                    $"{Format(metric.maximumBrakingSpeedIncreaseMetersPerSecond)}");
            }

            AssertRangeWithin(trials.Select(item => item.startFrames), 5f, "starter frames");
            AssertRangeWithin(trials.Select(item => item.idleAverageRpm), 50f, "idle average RPM");
            AssertRelativeRangeWithin(
                trials.Select(item => item.launchPeakSpeedMetersPerSecond),
                absoluteAllowance: 0.35f,
                relativeAllowance: 0.15f,
                label: "launch peak speed");
            AssertRelativeRangeWithin(
                trials.Select(item => item.launchDistanceMeters),
                absoluteAllowance: 0.5f,
                relativeAllowance: 0.15f,
                label: "launch distance");
            AssertRangeWithin(trials.Select(item => item.brakingFrames), 15f, "braking frames");
            AssertRelativeRangeWithin(
                trials.Select(item => item.brakingDistanceMeters),
                absoluteAllowance: 0.5f,
                relativeAllowance: 0.2f,
                label: "braking distance");

            evidence.trials = trials;
            evidence.repeatTrialsPassed = true;
            EndTelemetryCapture();
        }

        [UnityTest, Order(2)]
        public IEnumerator SurfaceContactsAndConfiguredResponseOrdering_AreExplicitAndFinite()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "surface-comparison");
            VehicleSurfaceType[] surfaceTypes =
            {
                VehicleSurfaceType.Paved,
                VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt,
                VehicleSurfaceType.Grass
            };

            VehicleSurfaceResponse[] responses = surfaceTypes
                .Select(context.Host.Config.GetSurfaceResponse)
                .ToArray();
            for (int index = 1; index < responses.Length; index++)
            {
                Assert.That(
                    responses[index - 1].FrictionMultiplier,
                    Is.GreaterThan(responses[index].FrictionMultiplier),
                    $"Friction ordering drifted between {surfaceTypes[index - 1]} and " +
                    $"{surfaceTypes[index]}.");
                Assert.That(
                    responses[index - 1].RollingResistanceMultiplier,
                    Is.LessThan(responses[index].RollingResistanceMultiplier),
                    $"Rolling-resistance ordering drifted between {surfaceTypes[index - 1]} and " +
                    $"{surfaceTypes[index]}.");
            }

            var metrics = new SurfaceMetricDto[surfaceTypes.Length];
            for (int surfaceIndex = 0; surfaceIndex < surfaceTypes.Length; surfaceIndex++)
            {
                VehicleSurfaceType surfaceType = surfaceTypes[surfaceIndex];
                VehicleSurfaceMetadataAuthoring surface = FindSurface(surfaceType);
                Assert.That(surface, Is.Not.Null, $"Missing authored {surfaceType} surface.");
                Collider collider = surface.GetComponent<Collider>();
                Assert.That(collider, Is.Not.Null);

                Bounds bounds = collider.bounds;
                TeleportChassis(
                    context,
                    new Vector3(
                        bounds.center.x,
                        bounds.max.y + CalculateEquilibriumAnchorHeight(context.Host.Config),
                        bounds.center.z),
                    Quaternion.identity);
                yield return WaitFixedFrames(12);
                AssertFinite(context, "surface-" + surfaceType, 12);

                int contactCount = 0;
                float compressionSum = 0f;
                for (int wheelIndex = 0; wheelIndex < context.Host.State.WheelCount; wheelIndex++)
                {
                    VehicleWheelState wheel = context.Host.State.GetWheelState(wheelIndex);
                    if (!wheel.HasContact)
                    {
                        continue;
                    }

                    contactCount++;
                    compressionSum += wheel.SuspensionCompression01;
                    Assert.That(
                        wheel.Surface,
                        Is.EqualTo(surfaceType),
                        $"{surfaceType} collider resolved as {wheel.Surface} on wheel {wheelIndex}.");
                }

                Assert.That(contactCount, Is.GreaterThanOrEqualTo(2));
                VehicleSurfaceResponse response = responses[surfaceIndex];
                context.Input.SetContinuousControls(
                    throttle: 0f,
                    clutchPedal: 1f,
                    brake: 0f,
                    steeringInput: 0f,
                    ignition: false,
                    starter: false);
                Rigidbody chassis = context.Backend.Chassis;
                chassis.linearVelocity = Vector3.forward * 3f;
                chassis.angularVelocity = Vector3.zero;
                chassis.WakeUp();
                float coastdownInitialSpeed = HorizontalSpeed(chassis);
                for (int frame = 0; frame < FixedFramesPerSecond; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "surface-coastdown-" + surfaceType, frame);
                }

                float coastdownFinalSpeed = HorizontalSpeed(chassis);
                float coastdownLoss = coastdownInitialSpeed - coastdownFinalSpeed;
                Assert.That(coastdownFinalSpeed,
                    Is.LessThanOrEqualTo(coastdownInitialSpeed + 0.15f));
                Assert.That(coastdownLoss, Is.GreaterThan(0f));
                metrics[surfaceIndex] = new SurfaceMetricDto
                {
                    surface = surfaceType.ToString(),
                    contactWheelCount = contactCount,
                    averageCompression01 = compressionSum / contactCount,
                    frictionMultiplier = response.FrictionMultiplier,
                    rollingResistanceMultiplier = response.RollingResistanceMultiplier,
                    coastdownInitialSpeedMetersPerSecond = coastdownInitialSpeed,
                    coastdownFinalSpeedMetersPerSecond = coastdownFinalSpeed,
                    coastdownLossMetersPerSecond = coastdownLoss
                };
                Debug.Log(
                    "M06A_PHYSX_METRICS scenario=surface-contact " +
                    $"surface={surfaceType} contactWheelCount={contactCount} " +
                    $"averageCompression01={Format(metrics[surfaceIndex].averageCompression01)} " +
                    $"frictionMultiplier={Format(response.FrictionMultiplier)} " +
                    $"rollingResistanceMultiplier={Format(response.RollingResistanceMultiplier)} " +
                    $"coastdownLossMetersPerSecond={Format(coastdownLoss)}");
            }

            evidence.surfaces = metrics;
            evidence.surfaceContactsPassed = true;
            EndTelemetryCapture();
        }

        [UnityTest, Order(3)]
        public IEnumerator Coastdown_ThreeNeutralTrialsLoseSpeedAndRemainFinite()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "coastdown");
            var trials = new CoastdownMetricDto[TrialCount];

            for (int trialIndex = 0; trialIndex < TrialCount; trialIndex++)
            {
                PreparePrototypeForTrial(context);
                yield return WaitFixedFrames(4);
                yield return StartEngineAndSelectFirstGear(
                    context,
                    "coastdown-" + (trialIndex + 1));

                yield return AccelerateStraightToMinimumSpeed(
                    context,
                    "coastdown-acceleration",
                    minimumSpeedMetersPerSecond: 1f,
                    maximumFrames: FixedFramesPerSecond * 6);

                context.Input.RequestGear(0);
                context.Input.SetContinuousControls(
                    throttle: 0f,
                    clutchPedal: 1f,
                    brake: 0f,
                    steeringInput: 0f,
                    ignition: true,
                    starter: false);
                yield return new WaitForFixedUpdate();
                Assert.That(context.Host.State.SelectedGear, Is.Zero);

                var metric = new CoastdownMetricDto
                {
                    trialIndex = trialIndex + 1,
                    initialSpeedMetersPerSecond = HorizontalSpeed(context.Backend.Chassis),
                    maximumSpeedMetersPerSecond = HorizontalSpeed(context.Backend.Chassis),
                    coastFrames = FixedFramesPerSecond * 3
                };
                trials[trialIndex] = metric;
                Assert.That(metric.initialSpeedMetersPerSecond, Is.GreaterThan(0.75f));

                for (int frame = 0; frame < metric.coastFrames; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "coastdown-neutral", frame);
                    metric.maximumSpeedMetersPerSecond = Mathf.Max(
                        metric.maximumSpeedMetersPerSecond,
                        HorizontalSpeed(context.Backend.Chassis));
                }

                metric.finalSpeedMetersPerSecond = HorizontalSpeed(context.Backend.Chassis);
                metric.speedLossMetersPerSecond =
                    metric.initialSpeedMetersPerSecond - metric.finalSpeedMetersPerSecond;
                metric.coastSeconds = metric.coastFrames * Time.fixedDeltaTime;
                metric.finalContactWheelCount = CountContactWheels(context.Host);
                Assert.That(
                    metric.maximumSpeedMetersPerSecond,
                    Is.LessThanOrEqualTo(metric.initialSpeedMetersPerSecond + 0.15f),
                    $"Coastdown trial {metric.trialIndex} accelerated in neutral: " +
                    $"initial={metric.initialSpeedMetersPerSecond:0.######}, " +
                    $"maximum={metric.maximumSpeedMetersPerSecond:0.######}.");
                Assert.That(
                    metric.speedLossMetersPerSecond,
                    Is.GreaterThan(0.02f),
                    $"Coastdown trial {metric.trialIndex} produced no measurable speed loss.");
                Assert.That(metric.finalContactWheelCount, Is.GreaterThanOrEqualTo(2));

                Debug.Log(
                    "M06A_PHYSX_METRICS scenario=coastdown " +
                    $"trial={metric.trialIndex} " +
                    $"initialSpeedMetersPerSecond={Format(metric.initialSpeedMetersPerSecond)} " +
                    $"maximumSpeedMetersPerSecond={Format(metric.maximumSpeedMetersPerSecond)} " +
                    $"finalSpeedMetersPerSecond={Format(metric.finalSpeedMetersPerSecond)} " +
                    $"speedLossMetersPerSecond={Format(metric.speedLossMetersPerSecond)} " +
                    $"coastFrames={metric.coastFrames} " +
                    $"coastSeconds={Format(metric.coastSeconds)} " +
                    $"finalContactWheelCount={metric.finalContactWheelCount}");
            }

            AssertRelativeRangeWithin(
                trials.Select(item => item.initialSpeedMetersPerSecond),
                absoluteAllowance: 0.35f,
                relativeAllowance: 0.15f,
                label: "coastdown initial speed");
            AssertRelativeRangeWithin(
                trials.Select(item => item.speedLossMetersPerSecond),
                absoluteAllowance: 0.2f,
                relativeAllowance: 0.25f,
                label: "coastdown speed loss");

            evidence.coastdownTrials = trials;
            evidence.coastdownPassed = true;
            EndTelemetryCapture();
        }

        [UnityTest, Order(4)]
        public IEnumerator RuntimeSteeringSlalomStep_ProducesBoundedYawAndLateralResponse()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "steering-slalom");
            runtimeFixtureRoot = new GameObject("M06A_RuntimeSteeringStepFixture");
            CreateSurfacePrimitive(
                runtimeFixtureRoot.transform,
                "M06A_SteeringFloor",
                new Vector3(30f, -0.2f, 0f),
                new Vector3(30f, 0.4f, 60f),
                VehicleSurfaceType.Paved);
            Physics.SyncTransforms();

            float equilibriumHeight = CalculateEquilibriumAnchorHeight(context.Host.Config);
            TeleportChassis(
                context,
                new Vector3(30f, equilibriumHeight, -18f),
                Quaternion.identity);
            yield return WaitFixedFrames(8);
            Assert.That(CountContactWheels(context.Host), Is.GreaterThanOrEqualTo(2));
            yield return StartEngineAndSelectFirstGear(context, "steering-step");

            yield return AccelerateStraightToMinimumSpeed(
                context,
                "steering-acceleration",
                minimumSpeedMetersPerSecond: 1f,
                maximumFrames: FixedFramesPerSecond * 6);

            Rigidbody chassis = context.Backend.Chassis;
            float stepInitialSpeed = HorizontalSpeed(chassis);
            Assert.That(stepInitialSpeed, Is.GreaterThan(0.8f));
            Vector3 stepStartPosition = chassis.position;
            Vector3 stepStartForward = Vector3.ProjectOnPlane(chassis.transform.forward, Vector3.up)
                .normalized;
            Vector3 stepStartRight = Vector3.Cross(Vector3.up, stepStartForward).normalized;
            var metric = new SteeringMetricDto
            {
                initialSpeedMetersPerSecond = stepInitialSpeed,
                minimumContactWheelCount = context.Host.Config.WheelCount
            };

            float[] steeringSteps = { 0.55f, -0.55f };
            int zeroContactStreak = 0;
            const int framesPerStep = FixedFramesPerSecond * 2;
            for (int stepIndex = 0; stepIndex < steeringSteps.Length; stepIndex++)
            {
                float steeringInput = steeringSteps[stepIndex];
                for (int frame = 0; frame < framesPerStep; frame++)
                {
                    float speed = HorizontalSpeed(chassis);
                    bool speedAboveEnvelope = speed > 3.25f;
                    context.Input.SetContinuousControls(
                        throttle: speedAboveEnvelope ? 0f : 0.3f,
                        clutchPedal: speedAboveEnvelope ? 1f : 0.72f,
                        brake: speedAboveEnvelope ? 0.2f : 0f,
                        steeringInput: steeringInput,
                        ignition: true,
                        starter: false);
                    yield return new WaitForFixedUpdate();
                    AssertFinite(context, "steering-step-" + stepIndex, frame);

                    speed = HorizontalSpeed(chassis);
                    metric.peakSpeedMetersPerSecond = Mathf.Max(
                        metric.peakSpeedMetersPerSecond,
                        speed);
                    metric.peakAngularSpeedRadiansPerSecond = Mathf.Max(
                        metric.peakAngularSpeedRadiansPerSecond,
                        chassis.angularVelocity.magnitude);
                    metric.peakVerticalSpeedMetersPerSecond = Mathf.Max(
                        metric.peakVerticalSpeedMetersPerSecond,
                        Mathf.Abs(chassis.linearVelocity.y));
                    metric.maximumPositiveSteeringAngleDegrees = Mathf.Max(
                        metric.maximumPositiveSteeringAngleDegrees,
                        context.Host.State.SteeringAngleDegrees);
                    metric.minimumNegativeSteeringAngleDegrees = Mathf.Min(
                        metric.minimumNegativeSteeringAngleDegrees,
                        context.Host.State.SteeringAngleDegrees);

                    Vector3 currentForward = Vector3.ProjectOnPlane(
                        chassis.transform.forward,
                        Vector3.up).normalized;
                    float signedYaw = Vector3.SignedAngle(
                        stepStartForward,
                        currentForward,
                        Vector3.up);
                    metric.maximumAbsoluteYawDegrees = Mathf.Max(
                        metric.maximumAbsoluteYawDegrees,
                        Mathf.Abs(signedYaw));
                    if (stepIndex == 1)
                    {
                        metric.maximumReverseStepYawDeltaDegrees = Mathf.Max(
                            metric.maximumReverseStepYawDeltaDegrees,
                            Mathf.Abs(Mathf.DeltaAngle(
                                metric.firstStepEndYawDegrees,
                                signedYaw)));
                    }

                    float lateral = Mathf.Abs(Vector3.Dot(
                        chassis.position - stepStartPosition,
                        stepStartRight));
                    metric.maximumLateralDisplacementMeters = Mathf.Max(
                        metric.maximumLateralDisplacementMeters,
                        lateral);

                    int contacts = CountContactWheels(context.Host);
                    metric.minimumContactWheelCount = Mathf.Min(
                        metric.minimumContactWheelCount,
                        contacts);
                    for (int wheelIndex = 0; wheelIndex < context.Host.State.WheelCount; wheelIndex++)
                    {
                        metric.maximumAbsoluteLateralSlip = Mathf.Max(
                            metric.maximumAbsoluteLateralSlip,
                            Mathf.Abs(context.Host.State.GetWheelState(wheelIndex).LateralSlip));
                    }
                    if (contacts == 0)
                    {
                        zeroContactStreak++;
                        metric.maximumZeroContactStreakFrames = Mathf.Max(
                            metric.maximumZeroContactStreakFrames,
                            zeroContactStreak);
                    }
                    else
                    {
                        zeroContactStreak = 0;
                    }
                }

                if (stepIndex == 0)
                {
                    Vector3 firstStepEndForward = Vector3.ProjectOnPlane(
                        chassis.transform.forward,
                        Vector3.up).normalized;
                    metric.firstStepEndYawDegrees = Vector3.SignedAngle(
                        stepStartForward,
                        firstStepEndForward,
                        Vector3.up);
                }
            }

            metric.finalSpeedMetersPerSecond = HorizontalSpeed(chassis);
            metric.forwardProgressMeters = Vector3.Dot(
                chassis.position - stepStartPosition,
                stepStartForward);
            metric.totalStepFrames = framesPerStep * steeringSteps.Length;
            Assert.That(metric.maximumPositiveSteeringAngleDegrees, Is.GreaterThan(2f));
            Assert.That(metric.minimumNegativeSteeringAngleDegrees, Is.LessThan(-2f));
            Assert.That(metric.maximumAbsoluteYawDegrees, Is.GreaterThan(0.75f));
            Assert.That(metric.maximumReverseStepYawDeltaDegrees, Is.GreaterThan(0.25f));
            Assert.That(metric.maximumLateralDisplacementMeters, Is.GreaterThan(0.1f));
            Assert.That(metric.forwardProgressMeters, Is.GreaterThan(0.5f));
            Assert.That(metric.minimumContactWheelCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(metric.maximumZeroContactStreakFrames, Is.LessThanOrEqualTo(5));
            Assert.That(metric.peakSpeedMetersPerSecond, Is.LessThanOrEqualTo(4f));
            Assert.That(metric.peakAngularSpeedRadiansPerSecond, Is.LessThanOrEqualTo(8f));
            Assert.That(metric.peakVerticalSpeedMetersPerSecond, Is.LessThanOrEqualTo(2f));
            Assert.That(metric.maximumLateralDisplacementMeters, Is.LessThanOrEqualTo(10f));
            Assert.That(float.IsNaN(metric.maximumAbsoluteLateralSlip), Is.False);
            Assert.That(float.IsInfinity(metric.maximumAbsoluteLateralSlip), Is.False);

            evidence.steering = metric;
            evidence.steeringStepPassed = true;
            EndTelemetryCapture();
            Debug.Log(
                "M06A_PHYSX_METRICS scenario=steering-slalom-step " +
                $"initialSpeedMetersPerSecond={Format(metric.initialSpeedMetersPerSecond)} " +
                $"finalSpeedMetersPerSecond={Format(metric.finalSpeedMetersPerSecond)} " +
                $"peakSpeedMetersPerSecond={Format(metric.peakSpeedMetersPerSecond)} " +
                $"maximumPositiveSteeringAngleDegrees=" +
                $"{Format(metric.maximumPositiveSteeringAngleDegrees)} " +
                $"minimumNegativeSteeringAngleDegrees=" +
                $"{Format(metric.minimumNegativeSteeringAngleDegrees)} " +
                $"maximumAbsoluteYawDegrees={Format(metric.maximumAbsoluteYawDegrees)} " +
                $"firstStepEndYawDegrees={Format(metric.firstStepEndYawDegrees)} " +
                $"maximumReverseStepYawDeltaDegrees=" +
                $"{Format(metric.maximumReverseStepYawDeltaDegrees)} " +
                $"maximumLateralDisplacementMeters=" +
                $"{Format(metric.maximumLateralDisplacementMeters)} " +
                $"maximumAbsoluteLateralSlip={Format(metric.maximumAbsoluteLateralSlip)} " +
                $"forwardProgressMeters={Format(metric.forwardProgressMeters)} " +
                $"minimumContactWheelCount={metric.minimumContactWheelCount} " +
                $"maximumZeroContactStreakFrames={metric.maximumZeroContactStreakFrames} " +
                $"peakAngularSpeedRadiansPerSecond=" +
                $"{Format(metric.peakAngularSpeedRadiansPerSecond)}");
        }

        [UnityTest, Order(5)]
        public IEnumerator RuntimeSuspensionBump_ProducesCompressionAndFiniteRecovery()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "suspension-bump");
            runtimeFixtureRoot = new GameObject("M06A_RuntimeSuspensionBumpFixture");
            CreateSurfacePrimitive(
                runtimeFixtureRoot.transform,
                "M06A_BumpFloor",
                new Vector3(30f, -0.2f, 0f),
                new Vector3(12f, 0.4f, 20f),
                VehicleSurfaceType.Paved);
            CreateSurfacePrimitive(
                runtimeFixtureRoot.transform,
                "M06A_80mmBump",
                new Vector3(30f, 0.0525f, 0f),
                new Vector3(12f, 0.08f, 0.7f),
                VehicleSurfaceType.Paved);
            Physics.SyncTransforms();

            float equilibriumHeight = CalculateEquilibriumAnchorHeight(context.Host.Config);
            TeleportChassis(
                context,
                new Vector3(30f, equilibriumHeight, -4f),
                Quaternion.identity);
            yield return WaitFixedFrames(12);
            float baselineCompression = AverageContactCompression(context.Host, out int baselineContacts);
            Assert.That(baselineContacts, Is.GreaterThanOrEqualTo(2));

            const float frontAxleLocalZ = 1.1669996f;
            TeleportChassis(
                context,
                new Vector3(30f, equilibriumHeight, -frontAxleLocalZ),
                Quaternion.identity);
            float peakCompression = 0f;
            float peakVerticalSpeed = 0f;
            int minimumBumpContacts = context.Host.Config.WheelCount;
            for (int frame = 0; frame < 10; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "runtime-bump", frame);
                peakVerticalSpeed = Mathf.Max(
                    peakVerticalSpeed,
                    Mathf.Abs(context.Backend.Chassis.linearVelocity.y));
                int contacts = CountContactWheels(context.Host);
                minimumBumpContacts = Mathf.Min(minimumBumpContacts, contacts);
                for (int wheelIndex = 0; wheelIndex < context.Host.State.WheelCount; wheelIndex++)
                {
                    VehicleWheelState wheel = context.Host.State.GetWheelState(wheelIndex);
                    if (wheel.HasContact)
                    {
                        peakCompression = Mathf.Max(
                            peakCompression,
                            wheel.SuspensionCompression01);
                    }
                }
            }

            Assert.That(minimumBumpContacts, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                peakCompression,
                Is.GreaterThan(baselineCompression + 0.1f),
                $"Runtime bump did not produce a measurable suspension response: " +
                $"baseline={baselineCompression:0.######}, peak={peakCompression:0.######}.");

            TeleportChassis(
                context,
                new Vector3(30f, equilibriumHeight, 4f),
                Quaternion.identity);
            float maximumRecoveryVerticalSpeed = 0f;
            for (int frame = 0; frame < FixedFramesPerSecond; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "bump-recovery", frame);
                maximumRecoveryVerticalSpeed = Mathf.Max(
                    maximumRecoveryVerticalSpeed,
                    Mathf.Abs(context.Backend.Chassis.linearVelocity.y));
            }

            float recoveredCompression = AverageContactCompression(
                context.Host,
                out int recoveredContacts);
            Assert.That(recoveredContacts, Is.GreaterThanOrEqualTo(2));
            Assert.That(recoveredCompression, Is.EqualTo(baselineCompression).Within(0.15f));
            Assert.That(Mathf.Abs(context.Backend.Chassis.linearVelocity.y), Is.LessThan(0.5f));

            evidence.bump = new BumpMetricDto
            {
                bumpHeightMeters = 0.08f,
                baselineAverageCompression01 = baselineCompression,
                peakCompression01 = peakCompression,
                peakVerticalSpeedMetersPerSecond = peakVerticalSpeed,
                recoveredAverageCompression01 = recoveredCompression,
                maximumRecoveryVerticalSpeedMetersPerSecond = maximumRecoveryVerticalSpeed,
                minimumBumpContactWheelCount = minimumBumpContacts,
                recoveredContactWheelCount = recoveredContacts
            };
            evidence.bumpRecoveryPassed = true;
            EndTelemetryCapture();
            Debug.Log(
                "M06A_PHYSX_METRICS scenario=suspension-bump " +
                $"bumpHeightMeters={Format(evidence.bump.bumpHeightMeters)} " +
                $"baselineAverageCompression01=" +
                $"{Format(evidence.bump.baselineAverageCompression01)} " +
                $"peakCompression01={Format(evidence.bump.peakCompression01)} " +
                $"peakVerticalSpeedMetersPerSecond=" +
                $"{Format(evidence.bump.peakVerticalSpeedMetersPerSecond)} " +
                $"recoveredAverageCompression01=" +
                $"{Format(evidence.bump.recoveredAverageCompression01)} " +
                $"recoveredContactWheelCount={evidence.bump.recoveredContactWheelCount}");
        }

        [UnityTest, Order(6)]
        public IEnumerator RuntimeHillStart_SixDegreeBrakeHoldAndLaunchRemainStable()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            BeginTelemetryCapture(context, "hill-start");
            runtimeFixtureRoot = new GameObject("M06A_RuntimeHillStartFixture");

            const float slopeDegrees = 6f;
            const float slopeLength = 30f;
            const float slopeX = 150f;
            const float slopeCenterZ = 15f;
            Quaternion slopeRotation = Quaternion.Euler(-slopeDegrees, 0f, 0f);
            float centerHeight = Mathf.Sin(slopeDegrees * Mathf.Deg2Rad) * slopeLength * 0.5f;
            GameObject slope = CreateSurfacePrimitive(
                runtimeFixtureRoot.transform,
                "M06A_HillStartSlope_6deg",
                new Vector3(slopeX, centerHeight - 0.2f, slopeCenterZ),
                new Vector3(8f, 0.4f, slopeLength),
                VehicleSurfaceType.Paved);
            slope.transform.rotation = slopeRotation;
            Physics.SyncTransforms();

            float equilibriumHeight = CalculateEquilibriumAnchorHeight(context.Host.Config);
            const float spawnZ = 4f;
            float spawnSurfaceHeight = Mathf.Tan(slopeDegrees * Mathf.Deg2Rad) * spawnZ;
            TeleportChassis(
                context,
                new Vector3(slopeX, spawnSurfaceHeight + equilibriumHeight, spawnZ),
                slopeRotation);
            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: false,
                starter: false);
            yield return WaitFixedFrames(20);
            AssertFinite(context, "hill-settle", 20);
            Assert.That(CountContactWheels(context.Host), Is.GreaterThanOrEqualTo(2));

            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: true);
            for (int frame = 0; frame < FixedFramesPerSecond * 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "hill-start-engine", frame);
                if (context.Host.State.EngineStatus == VehicleEngineStatus.Running)
                {
                    break;
                }
            }

            Assert.That(context.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            context.Input.RequestGear(1);
            context.Input.SetContinuousControls(
                throttle: 0.1f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return new WaitForFixedUpdate();
            Assert.That(context.Host.State.SelectedGear, Is.EqualTo(1));

            Rigidbody chassis = context.Backend.Chassis;
            Vector3 holdStart = chassis.position;
            int minimumContacts = context.Host.Config.WheelCount;
            for (int frame = 0; frame < FixedFramesPerSecond; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "hill-hold", frame);
                minimumContacts = Mathf.Min(minimumContacts, CountContactWheels(context.Host));
            }

            float holdDrift = HorizontalDistance(holdStart, chassis.position);
            Assert.That(holdDrift, Is.LessThanOrEqualTo(0.25f));

            Vector3 launchStart = chassis.position;
            Vector3 uphillDirection = slopeRotation * Vector3.forward;
            context.Input.SetContinuousControls(
                throttle: 0.78f,
                clutchPedal: 0.68f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            for (int frame = 0; frame < FixedFramesPerSecond * 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "hill-launch", frame);
                minimumContacts = Mathf.Min(minimumContacts, CountContactWheels(context.Host));
            }

            float uphillProgress = Vector3.Dot(chassis.position - launchStart, uphillDirection.normalized);
            Assert.That(uphillProgress, Is.GreaterThan(0.5f));
            Assert.That(minimumContacts, Is.GreaterThanOrEqualTo(2));
            evidence.hill = new HillMetricDto
            {
                slopeDegrees = slopeDegrees,
                holdDriftMeters = holdDrift,
                uphillProgressMeters = uphillProgress,
                minimumContactWheelCount = minimumContacts
            };
            evidence.hillStartPassed = true;
            EndTelemetryCapture();
            Debug.Log(
                "M06A_PHYSX_METRICS scenario=hill-start " +
                $"slopeDegrees={Format(slopeDegrees)} " +
                $"holdDriftMeters={Format(holdDrift)} " +
                $"uphillProgressMeters={Format(uphillProgress)} " +
                $"minimumContactWheelCount={minimumContacts}");
        }

        [UnityTest, Order(7)]
        public IEnumerator ProductionWorld_GarageExitMaintainsFiniteContactsAndLoadsNextCell()
        {
            yield return LoadScene("Bootstrap", LoadSceneMode.Single);
            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>();
            ProductionWorldStreamingService streaming =
                Object.FindFirstObjectByType<ProductionWorldStreamingService>();
            Assert.That(installer, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);

            int bootstrapTimeout = 300;
            while ((!installer.IsReady || streaming.IsStreaming ||
                    !streaming.IsCellLoaded("cell_0_-3")) && bootstrapTimeout-- > 0)
            {
                yield return null;
            }

            Assert.That(installer.IsReady, Is.True);
            Assert.That(streaming.IsCellLoaded("cell_0_-3"), Is.True);
            Assert.That(installer.SpawnedPlayer, Is.Not.Null);
            Assert.That(streaming.enabled, Is.True);
            installer.SpawnedPlayer.SetActive(false);

            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Additive);
            PrototypeContext context = ResolvePrototypeContext();
            PreparePrototypeAvailability(context);
            streaming.BindFocus(context.Backend.Chassis.transform);
            bool nextCellInitiallyLoaded = streaming.IsCellLoaded("cell_0_-2");
            Assert.That(
                nextCellInitiallyLoaded,
                Is.False,
                "The neighbor must begin unloaded so automatic runtime streaming is actually exercised.");
            BeginTelemetryCapture(context, "production-world-transition");

            WorldHingedArchitecture[] hinges = Object.FindObjectsByType<WorldHingedArchitecture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            WorldHingedArchitecture leftDoor = hinges.Single(hinge => hinge.name == "GarageDoorLeft");
            WorldHingedArchitecture rightDoor = hinges.Single(hinge => hinge.name == "GarageDoorRight");
            WorldHingedArchitecture leftGate =
                hinges.Single(hinge => hinge.name == "DrivewayGateLeft");
            WorldHingedArchitecture rightGate =
                hinges.Single(hinge => hinge.name == "DrivewayGateRight");
            leftDoor.SetOpen(true, immediate: true);
            rightDoor.SetOpen(true, immediate: true);
            leftGate.SetOpen(true, immediate: true);
            rightGate.SetOpen(true, immediate: true);
            Assert.That(leftDoor.OpenNormalized, Is.EqualTo(1f));
            Assert.That(rightDoor.OpenNormalized, Is.EqualTo(1f));
            Assert.That(leftGate.OpenNormalized, Is.EqualTo(1f));
            Assert.That(rightGate.OpenNormalized, Is.EqualTo(1f));

            TeleportChassis(context, ProductionGarageStart, Quaternion.identity);
            Assert.That(context.Backend.Chassis.position, Is.EqualTo(ProductionGarageStart));
            Assert.That(context.Backend.Chassis.rotation, Is.EqualTo(Quaternion.identity));
            yield return WaitFixedFrames(20);
            AssertFinite(context, "production-settle", 20);
            int startContacts = CountContactWheels(context.Host);
            bool sawPavedSurface = false;
            bool sawGravelSurface = false;
            ObserveProductionSurfaces(context.Host, ref sawPavedSurface, ref sawGravelSurface);
            Assert.That(
                startContacts,
                Is.GreaterThanOrEqualTo(2),
                "The requested production-garage start pose has no stable wheel support.");

            int startFrames = 0;
            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: true);
            for (int frame = 1; frame <= FixedFramesPerSecond * 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "production-start", frame);
                if (context.Host.State.EngineStatus == VehicleEngineStatus.Running)
                {
                    startFrames = frame;
                    break;
                }
            }

            Assert.That(context.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            context.Input.RequestGear(1);
            context.Input.SetContinuousControls(
                throttle: 0.1f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return new WaitForFixedUpdate();
            Assert.That(context.Host.State.SelectedGear, Is.EqualTo(1));

            context.Input.SetContinuousControls(
                throttle: 0.85f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitFixedFrames(FixedFramesPerSecond);
            Assert.That(context.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));

            int routeFrames = 0;
            int zeroContactStreak = 0;
            int maximumZeroContactStreak = 0;
            int minimumContactCount = context.Host.Config.WheelCount;
            float peakSpeed = 0f;
            float maximumLateralDeviation = 0f;
            float routeClutchPedal = 1f;
            const int maximumRouteFrames = FixedFramesPerSecond * 25;
            for (int frame = 1; frame <= maximumRouteFrames; frame++)
            {
                float speed = HorizontalSpeed(context.Backend.Chassis);
                if (speed > 2.4f)
                {
                    context.Input.SetContinuousControls(
                        throttle: 0f,
                        clutchPedal: 1f,
                        brake: 0.25f,
                        steeringInput: 0f,
                        ignition: true,
                        starter: false);
                }
                else
                {
                    ApplyLowSpeedDriveControls(
                        context,
                        speed,
                        ref routeClutchPedal,
                        highRpmClutchPedal: 0.42f,
                        clutchReleaseDeltaPerTick: 0.01f);
                }

                yield return new WaitForFixedUpdate();
                routeFrames = frame;
                AssertFinite(context, "production-route", frame);
                peakSpeed = Mathf.Max(peakSpeed, HorizontalSpeed(context.Backend.Chassis));
                maximumLateralDeviation = Mathf.Max(
                    maximumLateralDeviation,
                    Mathf.Abs(context.Backend.Chassis.position.x - ProductionGarageStart.x));

                int contacts = CountContactWheels(context.Host);
                ObserveProductionSurfaces(context.Host, ref sawPavedSurface, ref sawGravelSurface);
                minimumContactCount = Mathf.Min(minimumContactCount, contacts);
                if (contacts == 0)
                {
                    zeroContactStreak++;
                    maximumZeroContactStreak = Mathf.Max(
                        maximumZeroContactStreak,
                        zeroContactStreak);
                }
                else
                {
                    zeroContactStreak = 0;
                }

                if (context.Backend.Chassis.position.z >= ProductionCellBoundaryZ)
                {
                    break;
                }
            }

            float reachedZ = context.Backend.Chassis.position.z;
            float progress = reachedZ - ProductionGarageStart.z;
            Assert.That(
                reachedZ,
                Is.GreaterThanOrEqualTo(ProductionCellBoundaryZ),
                $"Scripted production route did not reach streaming boundary " +
                $"z={ProductionCellBoundaryZ:0.###}: " +
                $"reachedZ={reachedZ:0.######}, progress={progress:0.######}, " +
                $"frames={routeFrames}, speed={HorizontalSpeed(context.Backend.Chassis):0.######}, " +
                $"contacts={CountContactWheels(context.Host)}, " +
                $"engineStatus={context.Host.State.EngineStatus}, " +
                $"engineRpm={context.Host.State.EngineRpm:0.######}, " +
                $"gear={context.Host.State.SelectedGear}, " +
                $"clutchEngagement={context.Host.State.ClutchEngagement01:0.######}, " +
                $"clutchTorque={context.Host.State.ClutchTransferredTorqueNewtonMeters:0.######}.");
            Assert.That(
                maximumZeroContactStreak,
                Is.LessThanOrEqualTo(8),
                "Garage/driveway route lost every wheel contact for too many consecutive ticks.");
            Assert.That(
                maximumLateralDeviation,
                Is.LessThanOrEqualTo(1.1f),
                "Straight scripted route deviated outside the bounded garage opening corridor.");
            Assert.That(
                peakSpeed,
                Is.LessThanOrEqualTo(4f),
                "The bounded garage/driveway fixture exceeded its low-speed validation envelope.");
            Assert.That(
                minimumContactCount,
                Is.GreaterThanOrEqualTo(2),
                "The garage-to-boundary route dropped below two supporting wheels.");

            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            float boundaryHeight = context.Backend.Chassis.position.y;
            bool observedStreaming = streaming.IsStreaming;
            int minimumStreamingContactCount = context.Host.Config.WheelCount;
            float maximumStreamingVerticalDelta = 0f;
            int streamingTimeout = 300;
            while ((!streaming.IsCellLoaded("cell_0_-2") || streaming.IsStreaming) &&
                   streamingTimeout-- > 0)
            {
                observedStreaming |= streaming.IsStreaming;
                yield return null;
                AssertFinite(context, "production-streaming-load", streamingTimeout);
                int contacts = CountContactWheels(context.Host);
                minimumStreamingContactCount = Mathf.Min(
                    minimumStreamingContactCount,
                    contacts);
                maximumStreamingVerticalDelta = Mathf.Max(
                    maximumStreamingVerticalDelta,
                    Mathf.Abs(context.Backend.Chassis.position.y - boundaryHeight));
            }

            Assert.That(streamingTimeout, Is.GreaterThan(0), "Automatic neighbor streaming timed out.");
            Assert.That(streaming.IsCellLoaded("cell_0_-2"), Is.True);
            Assert.That(streaming.IsCellLoaded("cell_0_-3"), Is.True);
            Assert.That(streaming.Focus, Is.SameAs(context.Backend.Chassis.transform));
            Assert.That(
                minimumStreamingContactCount,
                Is.GreaterThanOrEqualTo(2),
                "Wheel support dropped below two contacts while the neighbor scene loaded.");
            Assert.That(
                maximumStreamingVerticalDelta,
                Is.LessThanOrEqualTo(0.35f),
                "Chassis vertical position changed excessively during neighbor loading.");

            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitFixedFrames(6);
            DisablePilotCellColliders();
            Physics.SyncTransforms();

            Scene nextCell = SceneManager.GetSceneByName("Production_cell_0_-2");
            Assert.That(nextCell.IsValid() && nextCell.isLoaded, Is.True);
            VehicleSurfaceMetadataAuthoring shoreApproach = nextCell
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleSurfaceMetadataAuthoring>(
                    includeInactive: true))
                .Single(surface =>
                    surface.name == "ShoreApproach" &&
                    surface.SurfaceType == VehicleSurfaceType.Grass);
            Collider shoreCollider = shoreApproach.GetComponent<Collider>();
            Assert.That(shoreCollider, Is.Not.Null);
            var probeRay = new Ray(
                new Vector3(ProductionGarageStart.x, 20f, NextCellContactProbeZ),
                Vector3.down);
            Assert.That(
                shoreCollider.Raycast(probeRay, out RaycastHit probeHit, 50f),
                Is.True,
                "The deterministic cell_0_-2 contact probe did not intersect ShoreApproach.");
            TeleportChassis(
                context,
                probeHit.point +
                Vector3.up * CalculateEquilibriumAnchorHeight(context.Host.Config),
                Quaternion.identity);

            float nextCellOnlyStartHeight = context.Backend.Chassis.position.y;
            int nextCellOnlyMinimumContacts = context.Host.Config.WheelCount;
            float nextCellOnlyMaximumVerticalDelta = 0f;
            bool sawNextCellGrassSurface = false;
            for (int frame = 0; frame < 12; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, "production-next-cell-only-support", frame);
                int contacts = CountContactWheels(context.Host);
                nextCellOnlyMinimumContacts = Mathf.Min(
                    nextCellOnlyMinimumContacts,
                    contacts);
                nextCellOnlyMaximumVerticalDelta = Mathf.Max(
                    nextCellOnlyMaximumVerticalDelta,
                    Mathf.Abs(context.Backend.Chassis.position.y - nextCellOnlyStartHeight));
                for (int wheelIndex = 0;
                     wheelIndex < context.Host.State.WheelCount;
                     wheelIndex++)
                {
                    VehicleWheelState wheel = context.Host.State.GetWheelState(wheelIndex);
                    sawNextCellGrassSurface |=
                        wheel.HasContact && wheel.Surface == VehicleSurfaceType.Grass;
                }
            }

            Assert.That(
                nextCellOnlyMinimumContacts,
                Is.GreaterThanOrEqualTo(2),
                "The vehicle was not supported by cell_0_-2 after cell_0_-3 colliders were disabled.");
            Assert.That(nextCellOnlyMaximumVerticalDelta, Is.LessThanOrEqualTo(0.35f));
            Assert.That(
                sawNextCellGrassSurface,
                Is.True,
                "The cell_0_-2 ShoreApproach Grass metadata was not resolved by wheel contacts.");

            int postStreamingContacts = CountContactWheels(context.Host);
            float postStreamingVerticalDelta = nextCellOnlyMaximumVerticalDelta;
            ObserveProductionSurfaces(context.Host, ref sawPavedSurface, ref sawGravelSurface);
            Assert.That(postStreamingContacts, Is.GreaterThanOrEqualTo(2));
            Assert.That(sawPavedSurface, Is.True, "Garage Paved surface was not resolved by wheel contacts.");
            Assert.That(sawGravelSurface, Is.True, "Driveway/road Gravel surface was not resolved by wheel contacts.");

            evidence.world = new WorldMetricDto
            {
                startX = ProductionGarageStart.x,
                startY = ProductionGarageStart.y,
                startZ = ProductionGarageStart.z,
                startRotationX = 0f,
                startRotationY = 0f,
                startRotationZ = 0f,
                startRotationW = 1f,
                startContactWheelCount = startContacts,
                engineStartFrames = startFrames,
                routeFrames = routeFrames,
                reachedZ = reachedZ,
                horizontalProgressMeters = progress,
                peakSpeedMetersPerSecond = peakSpeed,
                maximumLateralDeviationMeters = maximumLateralDeviation,
                minimumContactWheelCount = minimumContactCount,
                maximumZeroContactStreakFrames = maximumZeroContactStreak,
                postStreamingContactWheelCount = postStreamingContacts,
                postStreamingVerticalDeltaMeters = postStreamingVerticalDelta,
                minimumStreamingContactWheelCount = minimumStreamingContactCount,
                maximumStreamingVerticalDeltaMeters = maximumStreamingVerticalDelta,
                nextCellOnlyMinimumContactWheelCount = nextCellOnlyMinimumContacts,
                nextCellOnlyMaximumVerticalDeltaMeters = nextCellOnlyMaximumVerticalDelta,
                nextCellContactProbeZ = NextCellContactProbeZ,
                nextCellInitiallyLoaded = nextCellInitiallyLoaded,
                automaticStreamingObserved = observedStreaming,
                sawPavedSurface = sawPavedSurface,
                sawGravelSurface = sawGravelSurface,
                sawNextCellGrassSurface = sawNextCellGrassSurface,
                pilotCellLoaded = streaming.IsCellLoaded("cell_0_-3"),
                nextCellLoaded = streaming.IsCellLoaded("cell_0_-2"),
                garageDoorsOpen = leftDoor.OpenNormalized == 1f && rightDoor.OpenNormalized == 1f,
                drivewayGatesOpen =
                    leftGate.OpenNormalized == 1f && rightGate.OpenNormalized == 1f,
                playerDisabled = !installer.SpawnedPlayer.activeSelf
            };
            evidence.productionWorldPassed = true;
            EndTelemetryCapture();
            Debug.Log(
                "M06A_PHYSX_METRICS scenario=production-world-route " +
                $"start=({Format(evidence.world.startX)},{Format(evidence.world.startY)}," +
                $"{Format(evidence.world.startZ)}) routeFrames={routeFrames} " +
                $"reachedZ={Format(reachedZ)} " +
                $"horizontalProgressMeters={Format(progress)} " +
                $"peakSpeedMetersPerSecond={Format(peakSpeed)} " +
                $"maximumLateralDeviationMeters={Format(maximumLateralDeviation)} " +
                $"minimumContactWheelCount={minimumContactCount} " +
                $"maximumZeroContactStreakFrames={maximumZeroContactStreak} " +
                $"postStreamingContactWheelCount={postStreamingContacts} " +
                $"postStreamingVerticalDeltaMeters={Format(postStreamingVerticalDelta)} " +
                $"minimumStreamingContactWheelCount={minimumStreamingContactCount} " +
                $"maximumStreamingVerticalDeltaMeters=" +
                $"{Format(maximumStreamingVerticalDelta)} " +
                $"nextCellOnlyMinimumContactWheelCount={nextCellOnlyMinimumContacts} " +
                $"nextCellOnlyMaximumVerticalDeltaMeters=" +
                $"{Format(nextCellOnlyMaximumVerticalDelta)} " +
                $"surfaceTransition={sawPavedSurface && sawGravelSurface && sawNextCellGrassSurface} " +
                $"cell_0_-2_loaded={streaming.IsCellLoaded("cell_0_-2")}");

        }

        [UnityTest, Order(8)]
        public IEnumerator ScriptedPhysXPerformance_UsesRealBackendAndManualPhysicsSteps()
        {
            yield return LoadScene("VehicleSimulationPrototype", LoadSceneMode.Single);
            PrototypeContext context = ResolvePrototypeContext();
            PreparePrototypeAvailability(context);
            runtimeFixtureRoot = new GameObject("M06A_RuntimePhysXPerformanceFixture");
            CreateSurfacePrimitive(
                runtimeFixtureRoot.transform,
                "M06A_PhysXPerformanceFloor",
                new Vector3(300f, -0.2f, 0f),
                new Vector3(1000f, 0.4f, 1000f),
                VehicleSurfaceType.Paved);
            Physics.SyncTransforms();

            bool hostWasEnabled = context.Host.enabled;
            SimulationMode previousSimulationMode = Physics.simulationMode;
            try
            {
                context.Host.enabled = false;
                Physics.simulationMode = SimulationMode.Script;
                var result = new ScriptedPhysxPerformanceDto
                {
                    warmupTicksPerTrial = 128,
                    measuredTicksPerTrial = 800,
                    trialCount = 3,
                    fixedDeltaSeconds = Time.fixedDeltaTime,
                    substeps = context.Host.Config.SubstepCount,
                    rows = new[]
                    {
                        MeasureScriptedPhysXPerformance(
                            context,
                            captureTelemetryConsumer: false,
                            warmupTicksPerTrial: 128,
                            measuredTicksPerTrial: 800,
                            trialCount: 3),
                        MeasureScriptedPhysXPerformance(
                            context,
                            captureTelemetryConsumer: true,
                            warmupTicksPerTrial: 128,
                            measuredTicksPerTrial: 800,
                            trialCount: 3)
                    }
                };

                result.passed = result.rows.All(row => row.passedSanityCheck);
                Assert.That(result.rows.Length, Is.EqualTo(2));
                for (int rowIndex = 0; rowIndex < result.rows.Length; rowIndex++)
                {
                    ScriptedPhysxPerformanceRowDto row = result.rows[rowIndex];
                    string rowContext =
                        $"{row.metricId}: contacts={row.minimumContactWheelCount}, " +
                        $"invalid={row.invalidStateCount}, maxSpeed={row.maximumSpeedMetersPerSecond:0.######}, " +
                        $"allocated={row.allocatedBytesPerTick:0.######}, " +
                        $"rootMean={row.rootTickMilliseconds.mean:0.######}, " +
                        $"physicsMean={row.physicsSimulateMilliseconds.mean:0.######}.";
                    Assert.That(row.invalidStateCount, Is.Zero, rowContext);
                    Assert.That(
                        row.minimumContactWheelCount,
                        Is.GreaterThanOrEqualTo(2),
                        rowContext);
                    Assert.That(
                        row.allocatedBytesPerTick,
                        Is.LessThanOrEqualTo(0.25d),
                        rowContext);
                    Assert.That(row.measuredTicks, Is.EqualTo(2400), rowContext);
                    Assert.That(
                        row.combinedMilliseconds.mean,
                        Is.GreaterThan(0d),
                        rowContext);
                    Assert.That(
                        row.physicsSimulateMilliseconds.mean,
                        Is.GreaterThan(0d),
                        rowContext);
                }

                Assert.That(result.passed, Is.True);
                evidence.scriptedPhysxPerformance = result;
                evidence.scriptedPhysxPerformancePassed = true;
                Debug.Log(
                    "M06A_PHYSX_PERFORMANCE_METRICS " +
                    $"offCombinedMeanMs={result.rows[0].combinedMilliseconds.mean:0.######} " +
                    $"offPhysicsMeanMs={result.rows[0].physicsSimulateMilliseconds.mean:0.######} " +
                    $"onCombinedMeanMs={result.rows[1].combinedMilliseconds.mean:0.######} " +
                    $"onPhysicsMeanMs={result.rows[1].physicsSimulateMilliseconds.mean:0.######} " +
                    $"allocatedBytesPerTick={result.rows[1].allocatedBytesPerTick:0.######} " +
                    $"physicsProcessingAvailable={result.rows[1].physicsProcessingAvailable}");
            }
            finally
            {
                Physics.simulationMode = previousSimulationMode;
                context.Host.enabled = hostWasEnabled;
            }

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRuntimeFixturesAndStreaming()
        {
            if (telemetryRecorder != null)
            {
                telemetryRecorder.StopWithoutExport();
                telemetryRecorder = null;
                telemetryRelativePath = string.Empty;
            }

            if (runtimeFixtureRoot != null)
            {
                Object.Destroy(runtimeFixtureRoot);
                runtimeFixtureRoot = null;
                yield return null;
            }

            RestorePilotCellColliders();

            ProductionWorldStreamingService[] services =
                Object.FindObjectsByType<ProductionWorldStreamingService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (ProductionWorldStreamingService service in services)
            {
                service.enabled = false;
                int timeout = 120;
                while (service.IsStreaming && timeout-- > 0)
                {
                    yield return null;
                }

                if (!service.IsStreaming)
                {
                    yield return service.UnloadOwnedScenes();
                }
            }

            Scene prototype = SceneManager.GetSceneByName("VehicleSimulationPrototype");
            if (prototype.IsValid() && prototype.isLoaded && SceneManager.sceneCount > 1)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(prototype);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            foreach (GameCompositionRoot root in Object.FindObjectsByType<GameCompositionRoot>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(root.gameObject);
            }

            yield return null;
        }

        private static PrototypeContext ResolvePrototypeContext()
        {
            VehicleSimulationHost host = Object.FindFirstObjectByType<VehicleSimulationHost>();
            PrototypeRaycastWheelPhysicsBackend backend =
                Object.FindFirstObjectByType<PrototypeRaycastWheelPhysicsBackend>();
            AssemblyVehiclePrerequisiteAdapter adapter =
                Object.FindFirstObjectByType<AssemblyVehiclePrerequisiteAdapter>();
            Assert.That(host, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(host.IsInitialized, Is.True);
            Assert.That(host.Backend, Is.SameAs(backend));

            ScriptedVehicleInputSource input =
                host.gameObject.GetComponent<ScriptedVehicleInputSource>();
            if (input == null)
            {
                input = host.gameObject.AddComponent<ScriptedVehicleInputSource>();
            }

            host.SetInputSourceForTesting(input);
            var context = new PrototypeContext(host, backend, adapter, input);
            RecordConfigIdentity(host.Config);
            PreparePrototypeAvailability(context);
            return context;
        }

        private static void RecordConfigIdentity(VehicleSimulationConfig config)
        {
            string hash = Hash128.Compute(JsonUtility.ToJson(config)).ToString();
            VehicleCalibrationProfile profile =
                Resources.Load<VehicleCalibrationProfile>("M06A_VehicleCalibrationProfile");
            Assert.That(profile, Is.Not.Null, "M06A calibration profile is not available in Resources.");
            Assert.That(profile.Validate(out string profileFailure), Is.True, profileFailure);
            Assert.That(profile.VehicleConfiguration, Is.SameAs(config));
            string profileFingerprint = profile.ComputeContentFingerprint();
            if (string.IsNullOrWhiteSpace(evidence.vehicleConfigurationId))
            {
                evidence.vehicleConfigurationId = config.ConfigurationId;
                evidence.tuningSchemaVersion = config.TuningSchemaVersion;
                evidence.configJsonHash = hash;
                evidence.profileRevision = profile.Revision;
                evidence.profileFingerprint = profileFingerprint;
                return;
            }

            Assert.That(config.ConfigurationId, Is.EqualTo(evidence.vehicleConfigurationId));
            Assert.That(config.TuningSchemaVersion, Is.EqualTo(evidence.tuningSchemaVersion));
            Assert.That(hash, Is.EqualTo(evidence.configJsonHash));
            Assert.That(profile.Revision, Is.EqualTo(evidence.profileRevision));
            Assert.That(profileFingerprint, Is.EqualTo(evidence.profileFingerprint));
        }

        private static void PreparePrototypeForTrial(PrototypeContext context)
        {
            PreparePrototypeAvailability(context);
            context.Input.Clear();
            context.Host.ResetToSpawn();
        }

        private static void PreparePrototypeAvailability(PrototypeContext context)
        {
            context.Adapter.SetPrototypeAvailability(
                hasFuel: true,
                hasOil: true,
                hasCoolant: true,
                voltage: context.Host.Config.SupportSystems.NominalBatteryVoltage);
        }

        private void BeginTelemetryCapture(PrototypeContext context, string fileStem)
        {
            Assert.That(telemetryRecorder, Is.Null, "A previous telemetry capture was not closed.");
            telemetryRelativePath =
                "Docs/VehicleValidation/Telemetry/" + fileStem + ".csv";
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException(
                                     "Unity project root cannot be resolved for telemetry export.");
            string absolutePath = Path.Combine(
                projectRoot,
                telemetryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            telemetryRecorder = context.Host.GetComponent<VehicleTelemetryRecorder>();
            if (telemetryRecorder == null)
            {
                telemetryRecorder = context.Host.gameObject.AddComponent<VehicleTelemetryRecorder>();
            }

            telemetryRecorder.Configure(context.Host, 30000, absolutePath);
            Assert.That(telemetryRecorder.StartCapture(), Is.True);
        }

        private void EndTelemetryCapture()
        {
            Assert.That(telemetryRecorder, Is.Not.Null);
            string absolutePath = telemetryRecorder.StopCaptureAndExport();
            Assert.That(telemetryRecorder.Count, Is.GreaterThan(0));
            Assert.That(telemetryRecorder.CapacityReached, Is.False);
            Assert.That(File.Exists(absolutePath), Is.True);
            evidence.telemetryFiles.Add(telemetryRelativePath);
            telemetryRecorder = null;
            telemetryRelativePath = string.Empty;
        }

        private static void ObserveProductionSurfaces(
            VehicleSimulationHost host,
            ref bool sawPaved,
            ref bool sawGravel)
        {
            for (int wheelIndex = 0; wheelIndex < host.State.WheelCount; wheelIndex++)
            {
                VehicleWheelState wheel = host.State.GetWheelState(wheelIndex);
                if (!wheel.HasContact)
                {
                    continue;
                }

                sawPaved |= wheel.Surface == VehicleSurfaceType.Paved;
                sawGravel |= wheel.Surface == VehicleSurfaceType.Gravel;
            }
        }

        private static void ApplyLowSpeedDriveControls(
            PrototypeContext context,
            float speedMetersPerSecond,
            ref float clutchPedal,
            float highRpmClutchPedal = 0.58f,
            float clutchReleaseDeltaPerTick = 0.004f)
        {
            float rpm = context.Host.State.EngineRpm;
            float targetClutchPedal = rpm < 1250f
                ? 0.95f
                : speedMetersPerSecond < 0.3f
                    ? rpm > 3500f
                        ? highRpmClutchPedal
                        : 0.78f
                    : speedMetersPerSecond < 1.2f
                        ? 0.68f
                        : 0.62f;
            float maximumDelta = targetClutchPedal > clutchPedal
                ? 0.04f
                : clutchReleaseDeltaPerTick;
            clutchPedal = Mathf.MoveTowards(
                clutchPedal,
                targetClutchPedal,
                maximumDelta);
            context.Input.SetContinuousControls(
                throttle: speedMetersPerSecond < 1.2f ? 0.95f : 0.4f,
                clutchPedal: clutchPedal,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
        }

        private static IEnumerator AccelerateStraightToMinimumSpeed(
            PrototypeContext context,
            string phase,
            float minimumSpeedMetersPerSecond,
            int maximumFrames)
        {
            float clutchPedal = 1f;
            float maximumSpeed = HorizontalSpeed(context.Backend.Chassis);
            for (int frame = 1; frame <= maximumFrames; frame++)
            {
                ApplyLowSpeedDriveControls(context, maximumSpeed, ref clutchPedal);
                yield return new WaitForFixedUpdate();
                AssertFinite(context, phase, frame);
                Assert.That(
                    context.Host.State.EngineStatus,
                    Is.EqualTo(VehicleEngineStatus.Running),
                    phase + " stalled the engine during controlled pre-roll.");
                Assert.That(
                    CountContactWheels(context.Host),
                    Is.GreaterThanOrEqualTo(2),
                    phase + " lost the supported driving surface during controlled pre-roll.");
                maximumSpeed = Mathf.Max(
                    maximumSpeed,
                    HorizontalSpeed(context.Backend.Chassis));
                if (maximumSpeed >= minimumSpeedMetersPerSecond)
                {
                    yield break;
                }
            }

            Assert.Fail(
                $"{phase} did not reach {minimumSpeedMetersPerSecond:0.###} m/s " +
                $"within {maximumFrames} fixed frames; " +
                $"maximum={maximumSpeed:0.######} m/s, " +
                $"rpm={context.Host.State.EngineRpm:0.######}, " +
                $"gear={context.Host.State.SelectedGear}, " +
                $"clutch={context.Host.State.ClutchEngagement01:0.######}.");
        }

        private static ScriptedPhysxPerformanceRowDto MeasureScriptedPhysXPerformance(
            PrototypeContext context,
            bool captureTelemetryConsumer,
            int warmupTicksPerTrial,
            int measuredTicksPerTrial,
            int trialCount)
        {
            int measuredTicks = measuredTicksPerTrial * trialCount;
            var rootMilliseconds = new double[measuredTicks];
            var physicsMilliseconds = new double[measuredTicks];
            var combinedMilliseconds = new double[measuredTicks];
            int destinationIndex = 0;
            int invalidStateCount = 0;
            int minimumContactWheelCount = context.Host.Config.WheelCount;
            float maximumSpeedMetersPerSecond = 0f;
            long allocatedBytes = 0;
            long physicsProcessingTotalNanoseconds = 0;
            long physicsProcessingPeakNanoseconds = 0;
            int physicsProcessingSamples = 0;
            ProfilerRecorder physicsProcessingRecorder = default;
            try
            {
                try
                {
                    physicsProcessingRecorder = ProfilerRecorder.StartNew(
                        ProfilerCategory.Physics,
                        "Physics.Processing",
                        1);
                }
                catch (Exception)
                {
                    physicsProcessingRecorder = default;
                }

                for (int trialIndex = 0; trialIndex < trialCount; trialIndex++)
                {
                    TeleportChassis(
                        context,
                        new Vector3(
                            300f,
                            CalculateEquilibriumAnchorHeight(context.Host.Config),
                            -200f),
                        Quaternion.identity);
                    PreparePrototypeAvailability(context);
                    float clutchPedal = 1f;
                    for (int tick = 0; tick < warmupTicksPerTrial; tick++)
                    {
                        VehicleInputState warmupInput = BuildPhysXPerformanceInput(
                            context,
                            tick,
                            ref clutchPedal);
                        context.Host.Root.Tick(Time.fixedDeltaTime, warmupInput);
                        Physics.Simulate(Time.fixedDeltaTime);
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    for (int tick = 0; tick < measuredTicksPerTrial; tick++)
                    {
                        VehicleInputState input = BuildPhysXPerformanceInput(
                            context,
                            tick,
                            ref clutchPedal);
                        long combinedStarted = Stopwatch.GetTimestamp();
                        long rootStarted = Stopwatch.GetTimestamp();
                        context.Host.Root.Tick(Time.fixedDeltaTime, input);
                        long rootStopped = Stopwatch.GetTimestamp();
                        long physicsStarted = Stopwatch.GetTimestamp();
                        Physics.Simulate(Time.fixedDeltaTime);
                        long physicsStopped = Stopwatch.GetTimestamp();

                        double rootMs = ElapsedMilliseconds(rootStarted, rootStopped);
                        double physicsMs = ElapsedMilliseconds(physicsStarted, physicsStopped);
                        rootMilliseconds[destinationIndex] = rootMs;
                        physicsMilliseconds[destinationIndex] = physicsMs;
                        combinedMilliseconds[destinationIndex] =
                            ElapsedMilliseconds(combinedStarted, physicsStopped);
                        destinationIndex++;

                        if (!IsPerformanceStateFinite(context))
                        {
                            invalidStateCount++;
                        }

                        minimumContactWheelCount = Mathf.Min(
                            minimumContactWheelCount,
                            CountContactWheels(context.Host));
                        maximumSpeedMetersPerSecond = Mathf.Max(
                            maximumSpeedMetersPerSecond,
                            HorizontalSpeed(context.Backend.Chassis));
                        if (captureTelemetryConsumer)
                        {
                            performanceChecksum += CapturePerformanceTelemetry(
                                context.Host.Telemetry);
                        }

                        if (physicsProcessingRecorder.Valid &&
                            physicsProcessingRecorder.Count > 0)
                        {
                            long value = physicsProcessingRecorder.LastValue;
                            if (value > 0)
                            {
                                physicsProcessingTotalNanoseconds += value;
                                physicsProcessingPeakNanoseconds = Math.Max(
                                    physicsProcessingPeakNanoseconds,
                                    value);
                                physicsProcessingSamples++;
                            }
                        }
                    }

                    allocatedBytes +=
                        GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                }
            }
            finally
            {
                if (physicsProcessingRecorder.Valid)
                {
                    physicsProcessingRecorder.Dispose();
                }
            }

            double allocatedBytesPerTick = allocatedBytes / (double)measuredTicks;
            var row = new ScriptedPhysxPerformanceRowDto
            {
                metricId = captureTelemetryConsumer
                    ? "physx.scripted.telemetry_consumer_on"
                    : "physx.scripted.telemetry_consumer_off",
                telemetryConsumerEnabled = captureTelemetryConsumer,
                measuredTicks = measuredTicks,
                simulatedSeconds = measuredTicks * Time.fixedDeltaTime,
                rootTickMilliseconds = BuildTimingSummary(rootMilliseconds),
                physicsSimulateMilliseconds = BuildTimingSummary(physicsMilliseconds),
                combinedMilliseconds = BuildTimingSummary(combinedMilliseconds),
                allocatedBytes = allocatedBytes,
                allocatedBytesPerTick = allocatedBytesPerTick,
                minimumContactWheelCount = minimumContactWheelCount,
                maximumSpeedMetersPerSecond = maximumSpeedMetersPerSecond,
                invalidStateCount = invalidStateCount,
                physicsProcessingAvailable = physicsProcessingSamples > 0,
                physicsProcessingSampleCount = physicsProcessingSamples,
                physicsProcessingMeanMilliseconds = physicsProcessingSamples > 0
                    ? physicsProcessingTotalNanoseconds /
                      (double)physicsProcessingSamples / 1000000d
                    : 0d,
                physicsProcessingPeakMilliseconds =
                    physicsProcessingPeakNanoseconds / 1000000d,
                scope =
                    "PlayMode batch; real PrototypeRaycastWheelPhysicsBackend; " +
                    "manual root Tick plus blocking Physics.Simulate on the default physics scene."
            };
            row.passedSanityCheck =
                row.invalidStateCount == 0 &&
                row.minimumContactWheelCount >= 2 &&
                row.allocatedBytesPerTick <= 0.25d &&
                row.rootTickMilliseconds.mean > 0d &&
                row.physicsSimulateMilliseconds.mean > 0d;
            return row;
        }

        private static VehicleInputState BuildPhysXPerformanceInput(
            PrototypeContext context,
            int tick,
            ref float clutchPedal)
        {
            int phase = tick % 800;
            clutchPedal = 1f;
            if (phase < 50)
            {
                return new VehicleInputState(
                    0f, 1f, 0f, 0f, true, true, false, 0);
            }

            if (phase < 200)
            {
                return new VehicleInputState(
                    0.65f, 1f, 0f, 0f, true, false, false, 0);
            }

            if (phase < 350)
            {
                return new VehicleInputState(
                    0.25f, 1f, 0f, 0.55f, true, false, false, 0);
            }

            if (phase < 500)
            {
                return new VehicleInputState(
                    0.25f, 1f, 0f, -0.55f, true, false, false, 0);
            }

            if (phase < 650)
            {
                return new VehicleInputState(
                    0f, 1f, 1f, 0f, true, false, false, 0);
            }

            return new VehicleInputState(
                0f, 1f, 0f, 0f, true, false, false, 0);
        }

        private static bool IsPerformanceStateFinite(PrototypeContext context)
        {
            Rigidbody body = context.Backend.Chassis;
            return context.Host.State.IsFinite() &&
                   context.Host.Root.LastTickWasFinite &&
                   VehicleSimulationMath.IsFinite(body.position) &&
                   VehicleSimulationMath.IsFinite(body.linearVelocity) &&
                   VehicleSimulationMath.IsFinite(body.angularVelocity) &&
                   VehicleSimulationMath.IsFinite(body.rotation.x) &&
                   VehicleSimulationMath.IsFinite(body.rotation.y) &&
                   VehicleSimulationMath.IsFinite(body.rotation.z) &&
                   VehicleSimulationMath.IsFinite(body.rotation.w);
        }

        private static double CapturePerformanceTelemetry(VehicleTelemetry telemetry)
        {
            double value = telemetry.EngineRpm +
                           telemetry.EngineTorqueNewtonMeters +
                           telemetry.ClutchSlipRpm +
                           telemetry.VehicleSpeedMetersPerSecond +
                           telemetry.SteeringAngleDegrees +
                           telemetry.Brake01;
            for (int wheelIndex = 0; wheelIndex < telemetry.WheelCount; wheelIndex++)
            {
                VehicleWheelTelemetry wheel = telemetry.GetWheel(wheelIndex);
                value += wheel.NormalLoadNewtons +
                         wheel.LongitudinalSlip +
                         wheel.LateralSlip +
                         wheel.AngularSpeedRadiansPerSecond +
                         wheel.SuspensionCompression01 +
                         (int)wheel.Surface;
            }

            return value;
        }

        private static TimingSummaryDto BuildTimingSummary(double[] samples)
        {
            Array.Sort(samples);
            double total = 0d;
            for (int index = 0; index < samples.Length; index++)
            {
                total += samples[index];
            }

            int p95Index = Mathf.Clamp(
                Mathf.FloorToInt((samples.Length - 1) * 0.95f),
                0,
                samples.Length - 1);
            return new TimingSummaryDto
            {
                mean = total / samples.Length,
                p95 = samples[p95Index],
                maximum = samples[samples.Length - 1]
            };
        }

        private static double ElapsedMilliseconds(long started, long stopped) =>
            (stopped - started) * 1000d / Stopwatch.Frequency;

        private void DisablePilotCellColliders()
        {
            RestorePilotCellColliders();
            Scene pilotCell = SceneManager.GetSceneByName("Production_cell_0_-3");
            Assert.That(pilotCell.IsValid() && pilotCell.isLoaded, Is.True);
            GameObject[] roots = pilotCell.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Collider[] colliders = roots[rootIndex].GetComponentsInChildren<Collider>(
                    includeInactive: true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];
                    if (!collider.enabled)
                    {
                        continue;
                    }

                    collider.enabled = false;
                    disabledPilotCellColliders.Add(collider);
                }
            }

            Assert.That(
                disabledPilotCellColliders.Count,
                Is.GreaterThan(0),
                "No cell_0_-3 colliders were disabled for the next-cell-only support probe.");
        }

        private void RestorePilotCellColliders()
        {
            for (int index = 0; index < disabledPilotCellColliders.Count; index++)
            {
                Collider collider = disabledPilotCellColliders[index];
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            disabledPilotCellColliders.Clear();
        }

        private static IEnumerator StartEngineAndSelectFirstGear(
            PrototypeContext context,
            string phase)
        {
            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: true);
            for (int frame = 0; frame < FixedFramesPerSecond * 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                AssertFinite(context, phase + "-start", frame);
                if (context.Host.State.EngineStatus == VehicleEngineStatus.Running)
                {
                    break;
                }
            }

            Assert.That(
                context.Host.State.EngineStatus,
                Is.EqualTo(VehicleEngineStatus.Running),
                phase + " did not start the engine.");
            context.Input.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitFixedFrames(10);

            context.Input.RequestGear(1);
            context.Input.SetContinuousControls(
                throttle: 0.1f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return new WaitForFixedUpdate();
            Assert.That(context.Host.State.SelectedGear, Is.EqualTo(1));
            AssertFinite(context, phase + "-first-gear", 0);
        }

        private static void TeleportChassis(
            PrototypeContext context,
            Vector3 position,
            Quaternion rotation)
        {
            Rigidbody chassis = context.Backend.Chassis;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            chassis.position = position;
            chassis.rotation = rotation;
            context.Input.Clear();
            Physics.SyncTransforms();
            context.Host.ResetSimulation();
            chassis.WakeUp();
        }

        private static GameObject CreateSurfacePrimitive(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            VehicleSurfaceType surfaceType)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.position = position;
            result.transform.localScale = scale;
            result.AddComponent<VehicleSurfaceMetadataAuthoring>().Configure(surfaceType);
            return result;
        }

        private static VehicleSurfaceMetadataAuthoring FindSurface(VehicleSurfaceType type)
        {
            return Object.FindObjectsByType<VehicleSurfaceMetadataAuthoring>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .SingleOrDefault(surface => surface.SurfaceType == type);
        }

        private static float CalculateEquilibriumAnchorHeight(VehicleSimulationConfig config)
        {
            float staticLoadPerWheel = config.Dynamics.ProvisionalMassKilograms *
                                       Physics.gravity.magnitude /
                                       config.WheelCount;
            float compression = Mathf.Clamp(
                staticLoadPerWheel / config.Dynamics.SpringRateNewtonPerMeter,
                0f,
                config.Dynamics.SuspensionTravelMeters);
            return config.Dynamics.WheelRadiusMeters +
                   config.Dynamics.SuspensionRestLengthMeters -
                   compression;
        }

        private static float AverageContactCompression(
            VehicleSimulationHost host,
            out int contactCount)
        {
            float total = 0f;
            contactCount = 0;
            for (int index = 0; index < host.State.WheelCount; index++)
            {
                VehicleWheelState wheel = host.State.GetWheelState(index);
                if (!wheel.HasContact)
                {
                    continue;
                }

                total += wheel.SuspensionCompression01;
                contactCount++;
            }

            return contactCount > 0 ? total / contactCount : 0f;
        }

        private static int CountContactWheels(VehicleSimulationHost host)
        {
            int count = 0;
            for (int index = 0; index < host.State.WheelCount; index++)
            {
                if (host.State.GetWheelState(index).HasContact)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertFinite(PrototypeContext context, string phase, int frame)
        {
            Rigidbody body = context.Backend.Chassis;
            Assert.That(context.Host.State.IsFinite(), Is.True, $"Non-finite state in {phase}/{frame}.");
            Assert.That(context.Host.Root.LastTickWasFinite, Is.True, $"Non-finite tick in {phase}/{frame}.");
            Assert.That(VehicleSimulationMath.IsFinite(body.position), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.linearVelocity), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.angularVelocity), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.rotation.x), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.rotation.y), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.rotation.z), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(body.rotation.w), Is.True);
        }

        private static void AssertRangeWithin(
            System.Collections.Generic.IEnumerable<int> values,
            float allowedRange,
            string label)
        {
            int[] materialized = values.ToArray();
            float range = materialized.Max() - materialized.Min();
            Assert.That(range, Is.LessThanOrEqualTo(allowedRange), $"{label} range={range:0.######}.");
        }

        private static void AssertRangeWithin(
            System.Collections.Generic.IEnumerable<float> values,
            float allowedRange,
            string label)
        {
            float[] materialized = values.ToArray();
            float range = materialized.Max() - materialized.Min();
            Assert.That(range, Is.LessThanOrEqualTo(allowedRange), $"{label} range={range:0.######}.");
        }

        private static void AssertRelativeRangeWithin(
            System.Collections.Generic.IEnumerable<float> values,
            float absoluteAllowance,
            float relativeAllowance,
            string label)
        {
            float[] materialized = values.ToArray();
            float range = materialized.Max() - materialized.Min();
            float average = materialized.Average();
            float allowance = Mathf.Max(absoluteAllowance, average * relativeAllowance);
            Assert.That(
                range,
                Is.LessThanOrEqualTo(allowance),
                $"{label} range={range:0.######}, average={average:0.######}, " +
                $"allowance={allowance:0.######}.");
        }

        private static IEnumerator LoadScene(string sceneName, LoadSceneMode mode)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, mode);
            Assert.That(load, Is.Not.Null, "Scene is missing from Build Settings: " + sceneName);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator WaitFixedFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static float HorizontalSpeed(Rigidbody body) =>
            Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;

        private static float HorizontalDistance(Vector3 first, Vector3 second) =>
            Vector3.ProjectOnPlane(second - first, Vector3.up).magnitude;

        private static string Format(float value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string GetEvidenceAbsolutePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException(
                                     "Unity project root cannot be resolved.");
            return Path.Combine(
                projectRoot,
                EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WritePassingEvidence()
        {
            var output = new PhysicsRunEvidenceDto
            {
                schemaVersion = VehiclePhysicsValidationProtocol.EvidenceSchemaVersion,
                validatorId = VehiclePhysicsValidationProtocol.ValidatorId,
                passed = true,
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                fixedDeltaSeconds = Time.fixedDeltaTime,
                repeatTrialCount = TrialCount,
                vehicleConfigurationId = evidence.vehicleConfigurationId,
                tuningSchemaVersion = evidence.tuningSchemaVersion,
                configJsonHash = evidence.configJsonHash,
                profileRevision = evidence.profileRevision,
                profileFingerprint = evidence.profileFingerprint,
                trials = evidence.trials,
                surfaces = evidence.surfaces,
                coastdownTrials = evidence.coastdownTrials,
                steering = evidence.steering,
                bump = evidence.bump,
                hill = evidence.hill,
                telemetryFiles = evidence.telemetryFiles.ToArray(),
                productionWorld = evidence.world,
                scriptedPhysxPerformance = evidence.scriptedPhysxPerformance
            };

            string path = GetEvidenceAbsolutePath();
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ??
                throw new InvalidOperationException("Evidence directory cannot be resolved."));
            string temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                JsonUtility.ToJson(output, prettyPrint: true) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }

            Debug.Log("M06A_PHYSX_RUN_EVIDENCE_WRITTEN path=" + EvidenceRelativePath);
        }

        private sealed class PrototypeContext
        {
            public PrototypeContext(
                VehicleSimulationHost host,
                PrototypeRaycastWheelPhysicsBackend backend,
                AssemblyVehiclePrerequisiteAdapter adapter,
                ScriptedVehicleInputSource input)
            {
                Host = host;
                Backend = backend;
                Adapter = adapter;
                Input = input;
            }

            public VehicleSimulationHost Host { get; }
            public PrototypeRaycastWheelPhysicsBackend Backend { get; }
            public AssemblyVehiclePrerequisiteAdapter Adapter { get; }
            public ScriptedVehicleInputSource Input { get; }
        }

        private sealed class EvidenceAccumulator
        {
            public bool repeatTrialsPassed;
            public bool surfaceContactsPassed;
            public bool coastdownPassed;
            public bool steeringStepPassed;
            public bool bumpRecoveryPassed;
            public bool hillStartPassed;
            public bool productionWorldPassed;
            public bool scriptedPhysxPerformancePassed;
            public string vehicleConfigurationId = string.Empty;
            public int tuningSchemaVersion;
            public string configJsonHash = string.Empty;
            public string profileRevision = string.Empty;
            public string profileFingerprint = string.Empty;
            public TrialMetricDto[] trials = Array.Empty<TrialMetricDto>();
            public SurfaceMetricDto[] surfaces = Array.Empty<SurfaceMetricDto>();
            public CoastdownMetricDto[] coastdownTrials = Array.Empty<CoastdownMetricDto>();
            public SteeringMetricDto steering = new SteeringMetricDto();
            public BumpMetricDto bump = new BumpMetricDto();
            public HillMetricDto hill = new HillMetricDto();
            public WorldMetricDto world = new WorldMetricDto();
            public ScriptedPhysxPerformanceDto scriptedPhysxPerformance =
                new ScriptedPhysxPerformanceDto();
            public readonly List<string> telemetryFiles = new List<string>();
        }

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
            public SteeringMetricDto steering = new SteeringMetricDto();
            public BumpMetricDto bump = new BumpMetricDto();
            public HillMetricDto hill = new HillMetricDto();
            public string[] telemetryFiles = Array.Empty<string>();
            public WorldMetricDto productionWorld = new WorldMetricDto();
            public ScriptedPhysxPerformanceDto scriptedPhysxPerformance =
                new ScriptedPhysxPerformanceDto();
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
            public string metricId = string.Empty;
            public bool telemetryConsumerEnabled;
            public int measuredTicks;
            public float simulatedSeconds;
            public TimingSummaryDto rootTickMilliseconds = new TimingSummaryDto();
            public TimingSummaryDto physicsSimulateMilliseconds = new TimingSummaryDto();
            public TimingSummaryDto combinedMilliseconds = new TimingSummaryDto();
            public long allocatedBytes;
            public double allocatedBytesPerTick;
            public int minimumContactWheelCount;
            public float maximumSpeedMetersPerSecond;
            public int invalidStateCount;
            public bool physicsProcessingAvailable;
            public int physicsProcessingSampleCount;
            public double physicsProcessingMeanMilliseconds;
            public double physicsProcessingPeakMilliseconds;
            public bool passedSanityCheck;
            public string scope = string.Empty;
        }

        [Serializable]
        private sealed class TimingSummaryDto
        {
            public double mean;
            public double p95;
            public double maximum;
        }

        [Serializable]
        private sealed class TrialMetricDto
        {
            public int trialIndex;
            public int startFrames;
            public float startSeconds;
            public float idleAverageRpm;
            public float idleMaximumDeviationRpm;
            public float launchPeakSpeedMetersPerSecond;
            public float launchDistanceMeters;
            public float brakingInitialSpeedMetersPerSecond;
            public float brakingFinalSpeedMetersPerSecond;
            public float brakingDistanceMeters;
            public int brakingFrames;
            public float brakingSeconds;
            public float maximumBrakingSpeedIncreaseMetersPerSecond;
            public int minimumBrakingContactWheelCount;
            public int finalContactWheelCount;
        }

        [Serializable]
        private sealed class SurfaceMetricDto
        {
            public string surface = string.Empty;
            public int contactWheelCount;
            public float averageCompression01;
            public float frictionMultiplier;
            public float rollingResistanceMultiplier;
            public float coastdownInitialSpeedMetersPerSecond;
            public float coastdownFinalSpeedMetersPerSecond;
            public float coastdownLossMetersPerSecond;
        }

        [Serializable]
        private sealed class CoastdownMetricDto
        {
            public int trialIndex;
            public float initialSpeedMetersPerSecond;
            public float maximumSpeedMetersPerSecond;
            public float finalSpeedMetersPerSecond;
            public float speedLossMetersPerSecond;
            public int coastFrames;
            public float coastSeconds;
            public int finalContactWheelCount;
        }

        [Serializable]
        private sealed class SteeringMetricDto
        {
            public float initialSpeedMetersPerSecond;
            public float finalSpeedMetersPerSecond;
            public float peakSpeedMetersPerSecond;
            public float maximumPositiveSteeringAngleDegrees;
            public float minimumNegativeSteeringAngleDegrees;
            public float maximumAbsoluteYawDegrees;
            public float firstStepEndYawDegrees;
            public float maximumReverseStepYawDeltaDegrees;
            public float maximumLateralDisplacementMeters;
            public float maximumAbsoluteLateralSlip;
            public float forwardProgressMeters;
            public float peakAngularSpeedRadiansPerSecond;
            public float peakVerticalSpeedMetersPerSecond;
            public int minimumContactWheelCount;
            public int maximumZeroContactStreakFrames;
            public int totalStepFrames;
        }

        [Serializable]
        private sealed class BumpMetricDto
        {
            public float bumpHeightMeters;
            public float baselineAverageCompression01;
            public float peakCompression01;
            public float peakVerticalSpeedMetersPerSecond;
            public float recoveredAverageCompression01;
            public float maximumRecoveryVerticalSpeedMetersPerSecond;
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
            public float startX;
            public float startY;
            public float startZ;
            public float startRotationX;
            public float startRotationY;
            public float startRotationZ;
            public float startRotationW;
            public int startContactWheelCount;
            public int engineStartFrames;
            public int routeFrames;
            public float reachedZ;
            public float horizontalProgressMeters;
            public float peakSpeedMetersPerSecond;
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
            public bool pilotCellLoaded;
            public bool nextCellLoaded;
            public bool garageDoorsOpen;
            public bool drivewayGatesOpen;
            public bool playerDisabled;
        }
    }
}
