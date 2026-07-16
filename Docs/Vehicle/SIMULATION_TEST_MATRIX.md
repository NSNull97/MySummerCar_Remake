# Milestone 06 Simulation Test Matrix

## Execution status

Fresh Unity `6000.3.11f1` post-remediation/audio results were inspected on 2026-07-15 UTC (2026-07-16 local time):

- builder: PASS, marker `M06_VEHICLE_SIMULATION_BUILD_OK`, version `1.1.0`, build index `9`, four wheels;
- strict config/composition validator: PASS, marker `M06_VEHICLE_SIMULATION_VALIDATION_OK`;
- calibration: PASS, `crankTicks=15`, `idleRpm=908.023`, label `ProvisionalProjectTuning`, donor dynamic fixture `Missing`;
- focused EditMode: `18/18 PASS`, `0.1090665 s` (`M06_Audio_Final_EditMode`);
- focused PlayMode: `4/4 PASS`, `10.8416789 s` (`M06_Audio_Final_PlayMode`), with actual local loader/hash-ready marker in every case;
- full EditMode: `157/160 PASS`, exactly three known non-M06 failures, `44.4875523 s` (`M06_Audio_Final_FullEditMode`);
- full PlayMode: `31/31 PASS`, no failed/skipped tests, `49.4868179 s`; the audio-ready marker appears in all four M06 cases;
- performance audit: PASS in its isolated scope; `2.85809 / 4.09848 / 6.49453 us` per pure tick at `1/2/4` substeps, `7.71657 us` per backend iteration and `0.243964 us` per telemetry iteration, with zero measured allocations; `Physics.Processing` remains unavailable;
- first manual Unity drive smoke: `Partial pass / remediation required` — start/run, shifting, stall and RPM behavior worked; the vehicle crept at about `5 km/h` from startup and the environment visibly shook;
- post-remediation focused PlayMode: `4/4 PASS` after first-sample suspension-history initialization, planar speed reporting, bounded no-overshoot passive tire force, level-only startup settling, incline/external-wake guards and a detached smoothed chase camera;
- post-remediation user drive/audio recheck: `PASS / bounded basic-prototype acceptance recorded 2026-07-16`.

## EditMode inventory

Filter: `MSC.Tests.EditMode.VehicleSimulation`

| Test | Requirement | Status |
|---|---|---|
| `ProvisionalConfig_ValidatesAndCarriesExplicitProvenance` | Complete config, non-finite tuning rejection, and no false donor provenance | `PASS` |
| `TorqueCurve_InterpolatesAndClampsToEndpointSamples` | Torque interpolation | `PASS` |
| `VehicleAudioParameters_SanitizePresentationInputsWithoutAffectingSimulation` | Typed audio parameter sanitization and one-way presentation boundary | `PASS` |
| `StarterAndPrerequisiteFlags_ExposeExplicitCrankFailures` | Starter prerequisite behavior | `PASS` |
| `EngineFriction_ReducesAngularSpeedWithoutCombustion` | Friction/inertia | `PASS` |
| `Engine_StartsSettlesAtIdleAndStallsUnderLockedDrivetrainLoad` | Start, idle, stall | `PASS` |
| `Engine_IgnitionOffShutsDownAndRpmDecays` | Shutdown | `PASS` |
| `Clutch_UsesPedalEngagementSlipAndTorqueCapacity` | Engagement, slip and transfer capacity | `PASS` |
| `Gearbox_MapsNeutralReverseForwardAndRejectsInvalidShift` | Ratio mapping, neutral, reverse, invalid selection | `PASS` |
| `DifferentialBrakeSteeringAndSuspension_ProduceBoundedOutputs` | Default FL/FR (`0/1`) and remapped RL/RR (`2/3`) driven-pair routing, differential, `1800 N*m` brake default, steering and suspension calculations | `PASS` |
| `Root_SamplesAndAppliesBackendOncePerFixedTickRegardlessOfSubsteps` | Fixed-step/backend policy | `PASS` |
| `Root_ReportsAcceptedRejectedAndStableShiftStatusExplicitly` | Shift state | `PASS` |
| `FixedStepResults_AreEquivalentWithinNumericTolerance` | Frame/fixed-step tolerance | `PASS` |
| `StateDto_JsonRoundTripPreservesFiniteSimulationState` | Schema-1 serialization fixture | `PASS` |
| `Telemetry_ReflectsInputWheelSamplesAndPrerequisiteFlags` | Telemetry calculation | `PASS` |
| `Root_RejectsNonFiniteBackendStateExplicitly` | NaN/infinite failure policy | `PASS` |
| `Root_DoesNotAllocatePerTickAfterWarmup` | Normal tick allocation gate | `PASS` |
| `AssemblyAdapter_ReportsMissingSourceAndAvailabilityWithoutThrowing` | Explicit assembly/prerequisite failure | `PASS` |

Total authored EditMode tests: `18`.

## PlayMode inventory

Filter: `MSC.Tests.PlayMode.VehicleSimulation`

| Test | Requirement | Status |
|---|---|---|
| `PrototypeScene_BootsTypedHostBackendContactsAndSurfaceMetadata` | Scene boot, typed simulation/audio composition, wheel contact, route metadata, detached chase-camera target, gravity-plane speed, level startup rest and later `WakeUp + 0.05 m/s` not re-slept | `PASS` |
| `Starter_RejectsMissingFuelThenStartsAndSettlesAtIdle` | Missing prerequisite, start/idle, fixed-step audio event order `StarterEngaged -> StarterDisengaged -> EngineStarted`, and reset-during-`Cranking` delivery of `VehicleAudioEvent.Reset` | `PASS` |
| `UnpoweredVehicle_OnIncline_DoesNotUseStartupRestFreeze` | Unpowered proxy rolls on a six-degree slope instead of being pinned by startup stabilization | `PASS` |
| `Driveline_MovesBrakesStallsAndResetRecoversFiniteBody` | Unpowered rest stability, move, shift path, brake, stall, reset and finite physics | `PASS` |

Total authored PlayMode tests: `4`.

## Batch commands

Use Unity `6000.3.11f1` and replace `<UNITY_EXE>` and `<PROJECT>` with the configured local paths.

Build the generated content first:

```powershell
<UNITY_EXE> -batchmode -quit -projectPath <PROJECT> -buildTarget Win64 -executeMethod MSC.Editor.VehicleSimulation.VehicleSimulationPrototypeBuilder.RunBatch -logFile Logs/M06_Builder.log
```

Static config/composition validation:

```powershell
<UNITY_EXE> -batchmode -quit -projectPath <PROJECT> -buildTarget Win64 -executeMethod MSC.Editor.VehicleSimulation.VehicleSimulationPrototypeValidator.RunBatch -logFile Logs/M06_Validator.log
```

Focused EditMode:

```powershell
<UNITY_EXE> -batchmode -projectPath <PROJECT> -buildTarget Win64 -runTests -testPlatform EditMode -testFilter MSC.Tests.EditMode.VehicleSimulation -testResults TestResults/M06_EditMode.xml -logFile Logs/M06_EditMode.log
```

Focused PlayMode:

```powershell
<UNITY_EXE> -batchmode -projectPath <PROJECT> -buildTarget Win64 -runTests -testPlatform PlayMode -testFilter MSC.Tests.PlayMode.VehicleSimulation -testResults TestResults/M06_PlayMode.xml -logFile Logs/M06_PlayMode.log
```

Calibration fixture:

```powershell
<UNITY_EXE> -batchmode -quit -projectPath <PROJECT> -buildTarget Win64 -executeMethod MSC.Editor.VehicleSimulation.VehicleSimulationCalibrationRunner.RunBatch -logFile Logs/M06_Calibration.log
```

Performance audit:

```powershell
<UNITY_EXE> -batchmode -quit -projectPath <PROJECT> -buildTarget Win64 -executeMethod MSC.Editor.VehicleSimulation.VehicleSimulationPerformanceAudit.RunBatch -logFile Logs/M06_Performance.log
```

The performance command created `Docs/Vehicle/M06_SIMULATION_PERFORMANCE.json`. Its fresh `2026-07-15T19:37:34.5972700Z` capture records:

| Scope | Iterations | Cost | Allocated bytes |
|---|---:|---:|---:|
| Pure root, 1 substep | 10,000 ticks | `2.85809 us/tick` | `0` |
| Pure root, 2 substeps | 10,000 ticks | `4.09848 us/tick` | `0` |
| Pure root, 4 substeps | 10,000 ticks | `6.49453 us/tick` | `0` |
| Prototype backend `Sample + Apply` | 50,000 | `7.71657 us/iteration` | `0` |
| Telemetry buffer copy | 50,000 | `0.243964 us/iteration` | `0` |

`Physics.Processing` is `UnavailableNotMeasured`; the audit is not a standalone frame-rate measurement.

Inspected result locations:

- `Logs/M06_Final2_Builder.log`;
- `Logs/M06_Final3_Validator.log`;
- `Logs/M06_Final2_Calibration.log`;
- `Logs/M06_Final2_Performance.log`;
- `Logs/M06_Final3_EditMode.log` and `TestResults/M06_EditMode.xml`;
- `Logs/M06_Final2_PlayMode.log` and `TestResults/M06_PlayMode.xml`;
- `Logs/M06_Final2_FullEditMode.log` and `TestResults/M06_FullEditMode.xml`;
- `Logs/M06_Final2_FullPlayMode.log` and `TestResults/M06_FullPlayMode.xml`.

Fresh post-remediation/audio evidence:

- `Logs/M06_Audio_Builder.log`;
- `Logs/M06_Audio_Validator.log`;
- `Logs/M06_Audio_Calibration.log`;
- `Logs/M06_Audio_Performance.log`;
- `Logs/M06_Audio_EditMode.log` and `TestResults/M06_Audio_EditMode.xml`;
- `Logs/M06_Audio_PlayMode.log` and `TestResults/M06_Audio_PlayMode.xml`;
- `Logs/M06_Audio_FullEditMode.log` and `TestResults/M06_Audio_FullEditMode.xml`;
- `Logs/M06_Audio_FullPlayMode.log` and `TestResults/M06_Audio_FullPlayMode.xml`.

Final post-remediation evidence:

- `Logs/M06_Audio_Final_Validator.log`;
- `Logs/M06_Audio_Final_Performance.log`;
- `Logs/M06_Audio_Final_EditMode.log` and `TestResults/M06_Audio_Final_EditMode.xml`;
- `Logs/M06_Audio_Final_PlayMode.log` and `TestResults/M06_Audio_Final_PlayMode.xml`;
- `Logs/M06_Audio_Final_FullEditMode.log` and `TestResults/M06_Audio_Final_FullEditMode.xml`;
- `Logs/M06_Audio_Final_FullPlayMode.log` and `TestResults/M06_Audio_Final_FullPlayMode.xml`.

The final focused and full PlayMode logs record `M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips=7 source=ExternalDonorStaging hashes=Verified` in all four M06 cases. When local staging/configuration is absent or invalid, the backend intentionally remains silent and the PlayMode suite does not fail solely because optional diagnostic sound is unavailable.

Earlier remediation evidence remains in the corresponding ignored `M06_Stability_*` log/result files; the final expanded suite supersedes it with `4/4` focused coverage.

Ignored logs/XML remain local; the compact performance JSON is durable project documentation.

Run full EditMode and PlayMode regressions after focused tests. Preserve and report the known external baseline separately: two M3 neutral-sky assertions and the 04A1 donor-hash drift were the three known failures before M06.

## Static validator coverage

`VehicleSimulationPrototypeValidator` checks:

- required config, input and prototype scene assets;
- `RemakeDesignTarget` / `ProvisionalProjectTuning` provenance;
- derived wheel-geometry provenance and exact anchors;
- complete surface-response set;
- exact action map/actions;
- one 12-part M06 logical assembly fixture, one root, 11 mounts and required fasteners;
- one host/backend/reset/input/prerequisite composition;
- one detached smoothed chase camera targeting the dynamic proxy and not parented under its Rigidbody;
- one `VehicleAudioPresenter` and one `M06_LocalDiagnosticVehicleAudio` child using the typed `IVehicleAudioBackend` boundary;
- no serialized `AudioSource` or `AudioClip` dependency in the generated prototype scene;
- one configurable two-wheel differential pair; authored FL/FR (`0/1`) defaults;
- one separate dynamic non-kinematic proxy;
- presenter, overlay, recorder and four wheel bindings;
- paved/gravel/dirt/grass track markers;
- build indices Bootstrap `0`, production cells `6/8`, M06 appended at `9`;
- no prohibited donor/reference-only dependency.

Status: `PASS`.

The builder marker is:

```text
M06_VEHICLE_SIMULATION_BUILD_OK version=1.1.0 scene=Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity buildIndex=9 wheels=4
```

The strict validator marker is `M06_VEHICLE_SIMULATION_VALIDATION_OK`.

## Manual prototype checklist

Open `Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity` and enter Play Mode.

Controls:

- `I`: ignition toggle;
- `Enter`: hold starter;
- `W` / Up: throttle;
- `S` / Down: brake;
- `Left Shift`: clutch pedal;
- `A/D` or Left/Right: steering;
- `Q/E`: gear down/up;
- `Backspace`: reset.

Verify, without changing camera/timescale to mask a defect:

1. ignition off blocks running;
2. ignition on + held starter reaches running state;
3. starter release settles to finite idle;
4. select first with clutch depressed, release progressively and move from rest;
5. steering direction is correct and bounded;
6. brake reduces speed without explosive reversal;
7. a high-load/incorrect clutch case can stall;
8. reset returns a finite stationary proxy;
9. surface telemetry changes across paved, gravel, dirt and grass;
10. no visible NaN, tunnelling, runaway oscillation or wheel explosion occurs;
11. when local clips are available, starter and RPM layers are audible and fade on stop/stall without affecting vehicle state.

First manual result (2026-07-15/16): core start/run, gear selection, stall and RPM response were confirmed at prototype level. Startup creep of about `5 km/h` and visible environment shake were reported. They were not accepted as tuning: the defects were traced to synthetic first-sample suspension damping/full-velocity speed reporting, unstable low-speed passive-force correction and a camera parented to the Rigidbody.

Automated remediation result: focused PlayMode `4/4 PASS`. The backend initializes contact history without a first-frame damper impulse, reports gravity-plane speed, prevents passive force from overshooting through zero and settles only the level startup/reset case. A six-degree incline remains free to roll, and a later external `WakeUp()` plus `0.05 m/s` velocity is not re-slept. The camera is detached and smoothed with a stabilized horizon. Audio is sampled in `FixedUpdate`, the starter transition order is asserted, and reset while `Cranking` follows `VehicleSimulationHost.SimulationReset -> VehicleAudioPresenter -> VehicleAudioEvent.Reset`; `UnityAudioBackend` then calls `StopImmediately`.

Final manual result (2026-07-16): `PASS for the bounded basic prototype`. After the post-remediation/audio recheck, the user confirmed that everything is good for the basic prototype. This accepts the M06 checklist at prototype scope only; it does not promote provisional physics, donor-audio parity or temporary clips to production-ready status.

## Gate interpretation

The automated M06 gate passes:

- builder and strict validator pass;
- focused EditMode `18/18` and PlayMode `4/4` pass, including configured external clip loading and hash verification;
- full PlayMode `31/31` passes;
- all M06 tests pass inside the fresh full EditMode run;
- the three full-EditMode failures are known unrelated baselines: two M3 lighting assertions against the preserved user-owned `M3_NeutralVolume.asset`, and one frozen 04A1 donor-hash drift for `sharedassets3.assets/.resource`;
- calibration and isolated performance audit pass;
- no measured managed allocation appears in the audited loops.

The first bounded manual drive established a partial pass and generated two concrete defects; both now have expanded automated remediation coverage and focused PlayMode is `4/4 PASS`. The post-remediation start/move/steer/brake/stall/reset and local-audio recheck was accepted by the user on 2026-07-16. Automated and manual readiness for M06A is PASS. M06A has not begun.
