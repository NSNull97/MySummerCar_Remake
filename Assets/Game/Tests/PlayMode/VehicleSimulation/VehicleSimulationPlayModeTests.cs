using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleSimulation
{
    public sealed class VehicleSimulationPlayModeTests
    {
        private const int FixedFramesPerSecond = 50;

        private VehicleSimulationHost host;
        private PrototypeRaycastWheelPhysicsBackend backend;
        private AssemblyVehiclePrerequisiteAdapter adapter;
        private ScriptedVehicleInputSource scriptedInput;
        private VehiclePrototypeChaseCamera chaseCamera;
        private VehicleAudioPresenter audioPresenter;
        private UnityAudioBackend audioBackend;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "VehicleSimulationPrototype",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "M06 prototype scene is missing from Build Settings.");
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            host = UnityEngine.Object.FindFirstObjectByType<VehicleSimulationHost>();
            backend = UnityEngine.Object.FindFirstObjectByType<PrototypeRaycastWheelPhysicsBackend>();
            adapter = UnityEngine.Object.FindFirstObjectByType<AssemblyVehiclePrerequisiteAdapter>();
            chaseCamera = UnityEngine.Object.FindFirstObjectByType<VehiclePrototypeChaseCamera>();
            audioPresenter = UnityEngine.Object.FindFirstObjectByType<VehicleAudioPresenter>();
            audioBackend = UnityEngine.Object.FindFirstObjectByType<UnityAudioBackend>();
            Assert.That(host, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(chaseCamera, Is.Not.Null);
            Assert.That(audioPresenter, Is.Not.Null);
            Assert.That(audioBackend, Is.Not.Null);
            Assert.That(host.IsInitialized, Is.True);

            scriptedInput = host.gameObject.AddComponent<ScriptedVehicleInputSource>();
            host.SetInputSourceForTesting(scriptedInput);
            adapter.SetPrototypeAvailability(
                hasFuel: true,
                hasOil: true,
                hasCoolant: true,
                voltage: host.Config.SupportSystems.NominalBatteryVoltage);
            host.ResetToSpawn();
            scriptedInput.Clear();
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator PrototypeScene_BootsTypedHostBackendContactsAndSurfaceMetadata()
        {
            Assert.That(host.Backend, Is.SameAs(backend));
            Assert.That(host.Root, Is.Not.Null);
            Assert.That(host.Root.PowertrainGraph.NodeCount, Is.EqualTo(7));
            Assert.That(host.Config.Validate(out string failure), Is.True, failure);
            Assert.That(backend.IsConfigured, Is.True);
            Assert.That(backend.WheelCount, Is.EqualTo(4));
            Assert.That(backend.Chassis, Is.Not.Null);
            Assert.That(chaseCamera.Target, Is.SameAs(backend.Chassis.transform));
            Assert.That(chaseCamera.transform.IsChildOf(backend.Chassis.transform), Is.False);
            Assert.That(audioPresenter.SimulationHost, Is.SameAs(host));
            Assert.That(audioPresenter.BackendComponent, Is.SameAs(audioBackend));
            Assert.That(audioBackend.transform.IsChildOf(backend.Chassis.transform), Is.True);
            Assert.That(host.State.WheelCount, Is.EqualTo(4));
            Assert.That(host.State.IsFinite(), Is.True);

            Rigidbody stationaryChassis = backend.Chassis;
            stationaryChassis.linearVelocity = Vector3.up * 2f;
            Assert.That(
                backend.VehicleSpeedMetersPerSecond,
                Is.EqualTo(0f).Within(0.0001f),
                "Vertical suspension velocity must not be reported as road speed.");
            // Assigning linearVelocity wakes a sleeping Rigidbody. Reset after the
            // synthetic measurement so the following section exercises the real
            // spawn/reset path instead of an artificial external wake-up.
            host.ResetToSpawn();
            scriptedInput.Clear();
            yield return new WaitForFixedUpdate();
            Vector3 stationaryStart = stationaryChassis.position;
            float maximumHorizontalSpeed = 0f;
            float maximumVerticalSpeed = 0f;
            for (int frame = 0; frame < FixedFramesPerSecond * 2; frame++)
            {
                yield return new WaitForFixedUpdate();
                maximumHorizontalSpeed = Mathf.Max(
                    maximumHorizontalSpeed,
                    HorizontalSpeed(stationaryChassis));
                maximumVerticalSpeed = Mathf.Max(
                    maximumVerticalSpeed,
                    Mathf.Abs(stationaryChassis.linearVelocity.y));
            }

            Vector3 stationaryDelta = stationaryChassis.position - stationaryStart;
            yield return WaitForConfiguredLocalDiagnosticAudio();
            string stationaryDiagnostic =
                $"maxHorizontal={maximumHorizontalSpeed:0.###} " +
                $"maxVertical={maximumVerticalSpeed:0.###} " +
                $"finalVelocity={stationaryChassis.linearVelocity} " +
                $"angularVelocity={stationaryChassis.angularVelocity} " +
                $"delta={stationaryDelta} telemetrySpeed={host.State.VehicleSpeedMetersPerSecond:0.###}";
            Assert.That(
                maximumHorizontalSpeed,
                Is.LessThan(0.15f),
                stationaryDiagnostic);
            Assert.That(maximumVerticalSpeed, Is.LessThan(0.75f), stationaryDiagnostic);
            Assert.That(
                HorizontalDistance(stationaryChassis.position, stationaryStart),
                Is.LessThan(0.05f),
                stationaryDiagnostic);
            Assert.That(
                HorizontalSpeed(stationaryChassis),
                Is.LessThan(0.05f),
                stationaryDiagnostic);
            Assert.That(
                host.State.VehicleSpeedMetersPerSecond,
                Is.LessThan(0.05f),
                stationaryDiagnostic);

            stationaryChassis.WakeUp();
            stationaryChassis.linearVelocity = Vector3.right * 0.05f;
            Assert.That(HorizontalSpeed(stationaryChassis), Is.GreaterThan(0.01f));
            yield return new WaitForFixedUpdate();
            Assert.That(
                stationaryChassis.IsSleeping(),
                Is.False,
                "Startup rest stabilization must not absorb a later external impulse.");

            var prerequisites = new VehicleSimulationPrerequisites();
            adapter.Evaluate(
                new VehicleInputState(0f, 1f, 0f, 0f, true, false, false, 0),
                ref prerequisites);
            VehicleSimulationPrerequisiteFailure assemblyFailures =
                VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable |
                VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing |
                VehicleSimulationPrerequisiteFailure.StarterMissing |
                VehicleSimulationPrerequisiteFailure.BatteryMissing |
                VehicleSimulationPrerequisiteFailure.FuelUnavailable |
                VehicleSimulationPrerequisiteFailure.DrivetrainMissing |
                VehicleSimulationPrerequisiteFailure.DrivenWheelsMissing |
                VehicleSimulationPrerequisiteFailure.DrivenWheelsUnsecured;
            Assert.That(
                prerequisites.FailureFlags & assemblyFailures,
                Is.EqualTo(VehicleSimulationPrerequisiteFailure.None));

            yield return WaitFixedFrames(8);
            Assert.That(CountContactWheels(), Is.GreaterThanOrEqualTo(2));
            Assert.That(host.State.IsFinite(), Is.True);

            VehicleSurfaceMetadataAuthoring gravel = FindSurface(VehicleSurfaceType.Gravel);
            Assert.That(gravel, Is.Not.Null, "The bounded M06 route has no typed gravel segment.");
            Collider gravelCollider = gravel.GetComponent<Collider>();
            Assert.That(gravelCollider, Is.Not.Null);
            Bounds gravelBounds = gravelCollider.bounds;
            Rigidbody chassis = backend.Chassis;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            chassis.position = new Vector3(
                gravelBounds.center.x,
                gravelBounds.max.y + 0.55f,
                gravelBounds.center.z);
            chassis.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            host.ResetSimulation();

            yield return WaitFixedFrames(8);
            int gravelContacts = 0;
            for (int wheelIndex = 0; wheelIndex < host.State.WheelCount; wheelIndex++)
            {
                VehicleWheelState wheel = host.State.GetWheelState(wheelIndex);
                if (!wheel.HasContact)
                {
                    continue;
                }

                gravelContacts++;
                Assert.That(wheel.Surface, Is.EqualTo(VehicleSurfaceType.Gravel));
            }

            Assert.That(gravelContacts, Is.GreaterThanOrEqualTo(2));
            Assert.That(host.Root.LastTickWasFinite, Is.True);
        }

        [UnityTest]
        public IEnumerator Starter_RejectsMissingFuelThenStartsAndSettlesAtIdle()
        {
            var recordingAudio = host.gameObject.AddComponent<RecordingVehicleAudioBackend>();
            audioPresenter.enabled = false;
            audioPresenter.Configure(host, recordingAudio);
            Assert.That(audioPresenter.TryInitialize(out string audioFailure), Is.True, audioFailure);
            audioPresenter.enabled = true;

            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: true);
            yield return WaitForEngineStatus(
                VehicleEngineStatus.Cranking,
                FixedFramesPerSecond);
            Assert.That(host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Cranking));

            host.ResetToSpawn();
            Assert.That(host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(recordingAudio.Events, Is.Not.Empty);
            Assert.That(
                recordingAudio.Events[recordingAudio.Events.Count - 1],
                Is.EqualTo(VehicleAudioEvent.Reset),
                "A runtime vehicle reset must stop the active starter/engine mix immediately.");
            scriptedInput.Clear();
            recordingAudio.Events.Clear();
            yield return new WaitForFixedUpdate();

            adapter.SetPrototypeAvailability(
                hasFuel: false,
                hasOil: true,
                hasCoolant: true,
                voltage: host.Config.SupportSystems.NominalBatteryVoltage);
            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: true);

            yield return WaitFixedFrames(FixedFramesPerSecond);

            Assert.That(host.State.EngineStatus, Is.Not.EqualTo(VehicleEngineStatus.Running));
            Assert.That(host.State.EngineRpm, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                host.Root.Prerequisites.HasAny(
                    VehicleSimulationPrerequisiteFailure.FuelUnavailable),
                Is.True);

            host.ResetToSpawn();
            scriptedInput.Clear();
            adapter.SetPrototypeAvailability(
                hasFuel: true,
                hasOil: true,
                hasCoolant: true,
                voltage: host.Config.SupportSystems.NominalBatteryVoltage);

            yield return StartAndSettleAtIdle();

            Assert.That(host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(
                host.State.EngineRpm,
                Is.EqualTo(host.Config.Engine.IdleTargetRpm).Within(100f));
            Assert.That(host.State.IsFinite(), Is.True);

            int starterEngaged = recordingAudio.Events.IndexOf(VehicleAudioEvent.StarterEngaged);
            int starterDisengaged = recordingAudio.Events.IndexOf(VehicleAudioEvent.StarterDisengaged);
            int engineStarted = recordingAudio.Events.IndexOf(VehicleAudioEvent.EngineStarted);
            Assert.That(starterEngaged, Is.GreaterThanOrEqualTo(0));
            Assert.That(starterDisengaged, Is.GreaterThan(starterEngaged));
            Assert.That(engineStarted, Is.GreaterThan(starterDisengaged));
        }

        [UnityTest]
        public IEnumerator UnpoweredVehicle_OnIncline_DoesNotUseStartupRestFreeze()
        {
            VehicleSurfaceMetadataAuthoring paved = FindSurface(VehicleSurfaceType.Paved);
            Assert.That(paved, Is.Not.Null);
            Transform slope = paved.transform;
            slope.rotation = Quaternion.Euler(6f, 0f, 0f);

            VehicleDynamicsConfig dynamics = host.Config.Dynamics;
            float staticLoadPerWheel = dynamics.ProvisionalMassKilograms *
                                       Physics.gravity.magnitude /
                                       host.Config.WheelCount;
            float staticCompression = Mathf.Clamp(
                staticLoadPerWheel / dynamics.SpringRateNewtonPerMeter,
                0f,
                dynamics.SuspensionTravelMeters);
            float equilibriumAnchorHeight = dynamics.WheelRadiusMeters +
                                            dynamics.SuspensionRestLengthMeters -
                                            staticCompression;
            Vector3 surfaceTopCenter = slope.TransformPoint(Vector3.up * 0.5f);

            Rigidbody chassis = backend.Chassis;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;
            chassis.position = surfaceTopCenter + slope.up * equilibriumAnchorHeight;
            chassis.rotation = slope.rotation;
            Physics.SyncTransforms();
            host.ResetSimulation();
            scriptedInput.Clear();

            yield return WaitFixedFrames(2);
            Vector3 startPosition = chassis.position;
            yield return WaitFixedFrames(25);

            Assert.That(
                chassis.IsSleeping(),
                Is.False,
                "Startup rest stabilization must not pin an unpowered vehicle to an incline.");
            Assert.That(
                HorizontalDistance(chassis.position, startPosition),
                Is.GreaterThan(0.02f),
                "An unpowered vehicle must remain free to roll on a six-degree incline.");
        }

        [UnityTest]
        public IEnumerator Driveline_MovesBrakesStallsAndResetRecoversFiniteBody()
        {
            yield return StartAndSettleAtIdle();

            Rigidbody chassis = backend.Chassis;
            Vector3 startPosition = chassis.position;
            scriptedInput.RequestGear(1);
            scriptedInput.SetContinuousControls(
                throttle: 0.2f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return new WaitForFixedUpdate();
            Assert.That(host.State.SelectedGear, Is.EqualTo(1));

            scriptedInput.SetContinuousControls(
                throttle: 0.9f,
                clutchPedal: 0.7f,
                brake: 0f,
                steeringInput: 0.25f,
                ignition: true,
                starter: false);
            yield return WaitUntilHorizontalDistanceExceeds(
                startPosition,
                0.4f,
                FixedFramesPerSecond * 3);

            float movingSpeed = HorizontalSpeed(chassis);
            Assert.That(movingSpeed, Is.GreaterThan(0.5f));
            Assert.That(HorizontalDistance(chassis.position, startPosition), Is.GreaterThan(0.4f));
            Assert.That(Mathf.Abs(host.State.SteeringAngleDegrees), Is.GreaterThan(0f));

            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitUntilSpeedBelow(movingSpeed * 0.45f, FixedFramesPerSecond * 3);
            Assert.That(
                HorizontalSpeed(chassis),
                Is.LessThan(movingSpeed * 0.45f));
            Assert.That(host.State.BrakeTorqueNewtonMeters, Is.GreaterThan(0f));

            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 0f,
                brake: 1f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitForEngineStatus(
                VehicleEngineStatus.Stalled,
                FixedFramesPerSecond * 3);
            Assert.That(
                host.State.EngineStatus,
                Is.EqualTo(VehicleEngineStatus.Stalled),
                $"rpm={host.State.EngineRpm:0.###} horizontalSpeed={HorizontalSpeed(chassis):0.###} " +
                $"gear={host.State.SelectedGear} clutchTorque={host.State.ClutchTransferredTorqueNewtonMeters:0.###} " +
                $"clutchSlip={host.State.ClutchSlipRpm:0.###} prerequisites={host.Root.Prerequisites.FailureFlags}");

            VehicleResetController reset = host.GetComponent<VehicleResetController>();
            Assert.That(reset, Is.Not.Null);
            chassis.linearVelocity = new Vector3(50f, 25f, -30f);
            chassis.angularVelocity = new Vector3(10f, -20f, 30f);
            host.ResetToSpawn();

            Assert.That(chassis.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(chassis.angularVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(chassis.position, reset.ResetPose.position),
                Is.LessThan(0.001f));
            Assert.That(host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(host.State.IsFinite(), Is.True);

            yield return new WaitForFixedUpdate();
            Assert.That(VehicleSimulationMath.IsFinite(chassis.position), Is.True);
            Assert.That(VehicleSimulationMath.IsFinite(chassis.linearVelocity), Is.True);
            Assert.That(HorizontalSpeed(chassis), Is.LessThan(0.1f));
            Assert.That(host.State.VehicleSpeedMetersPerSecond, Is.LessThan(0.1f));
            Assert.That(host.Root.LastTickWasFinite, Is.True);
        }

        private IEnumerator StartAndSettleAtIdle()
        {
            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: true);
            yield return WaitForEngineStatus(
                VehicleEngineStatus.Running,
                FixedFramesPerSecond * 3);
            Assert.That(host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));

            scriptedInput.SetContinuousControls(
                throttle: 0f,
                clutchPedal: 1f,
                brake: 0f,
                steeringInput: 0f,
                ignition: true,
                starter: false);
            yield return WaitFixedFrames(FixedFramesPerSecond * 2);
        }

        private IEnumerator WaitForEngineStatus(VehicleEngineStatus expected, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (host.State.EngineStatus == expected)
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator WaitUntilHorizontalDistanceExceeds(
            Vector3 startPosition,
            float targetDistance,
            int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (HorizontalDistance(backend.Chassis.position, startPosition) > targetDistance)
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator WaitUntilSpeedBelow(float targetSpeed, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (HorizontalSpeed(backend.Chassis) < targetSpeed)
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitFixedFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static float HorizontalSpeed(Rigidbody body)
        {
            return Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
        }

        private IEnumerator WaitForConfiguredLocalDiagnosticAudio()
        {
#if UNITY_EDITOR
            string configurationPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Config/DonorPaths.local.json"));
            if (!File.Exists(configurationPath))
            {
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!audioBackend.IsReady &&
                   string.IsNullOrEmpty(audioBackend.FailureReason) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!audioBackend.IsReady)
            {
                Debug.LogWarning(
                    "Configured local diagnostic audio is unavailable; " +
                    "the required silent fallback remains active. " +
                    audioBackend.FailureReason);
            }
#else
            yield break;
#endif
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector3.ProjectOnPlane(first - second, Vector3.up).magnitude;
        }

        private int CountContactWheels()
        {
            int count = 0;
            for (int wheelIndex = 0; wheelIndex < host.State.WheelCount; wheelIndex++)
            {
                if (host.State.GetWheelState(wheelIndex).HasContact)
                {
                    count++;
                }
            }

            return count;
        }

        private static VehicleSurfaceMetadataAuthoring FindSurface(VehicleSurfaceType type)
        {
            VehicleSurfaceMetadataAuthoring[] surfaces =
                UnityEngine.Object.FindObjectsByType<VehicleSurfaceMetadataAuthoring>(
                    FindObjectsSortMode.None);
            for (int index = 0; index < surfaces.Length; index++)
            {
                if (surfaces[index].SurfaceType == type)
                {
                    return surfaces[index];
                }
            }

            return null;
        }

        private sealed class RecordingVehicleAudioBackend : MonoBehaviour, IVehicleAudioBackend
        {
            public bool IsReady => true;

            public string FailureReason => string.Empty;

            public List<VehicleAudioEvent> Events { get; } = new List<VehicleAudioEvent>();

            public VehicleAudioParameters LastParameters { get; private set; } =
                VehicleAudioParameters.Silent;

            public void SetVehicleParameters(in VehicleAudioParameters parameters)
            {
                LastParameters = parameters;
            }

            public void PostVehicleEvent(VehicleAudioEvent audioEvent)
            {
                Events.Add(audioEvent);
            }
        }
    }
}
