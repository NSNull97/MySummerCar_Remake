# Milestone 06 — Vehicle Simulation Prototype Report

Date: 2026-07-15; post-manual remediation updated 2026-07-16

Unity: `6000.3.11f1`

Status: **bounded Milestone 06 gate PASS; automated validation and the post-remediation drive/audio recheck are complete, with user acceptance recorded on 2026-07-16**.

## Scope

Only `Prompts/06_VEHICLE_SIMULATION.md` was implemented. The work establishes a separated simulation prototype and an isolated graybox route. It does not begin M06A, change production world geometry, add weather/UI, implement production audio, or claim final realism. After the first manual drive, the user authorized a bounded Editor-only diagnostic layer using original Satsuma clips from frozen external staging; that local aid is not production audio and is not a build dependency.

## Inspected baseline

- `AGENTS.md`, all milestone reports through M05B.1 and the M06/M06A prompts;
- M05 assembly architecture, save DTOs and tests;
- 04B reference database, fixtures, source map and missing queue;
- M05B/M05B.1 world, collision, streaming and performance evidence;
- save, audio, performance, testing and porting documentation;
- current Git state, including the preserved user-owned edit to `M3_NeutralVolume.asset`.

The M05B.1 gate is `PilotGate`, so M06 was allowed. M05B.1 did not provide a production driving route or an isolated physics CPU counter.

## Architecture implemented

`MSC.Vehicle.Simulation` now owns config, state, input, prerequisite/backend contracts, telemetry, the explicit powertrain topology and small simulation nodes. It has no project assembly reference.

`MSC.Vehicle.Runtime` composes the pure model with:

- `VehicleSimulationHost` at Unity fixed step;
- `AssemblyVehiclePrerequisiteAdapter`;
- `VehicleInputRouter` and dedicated Input System asset;
- `PrototypeRaycastWheelPhysicsBackend`;
- reset, wheel presentation, telemetry overlay and recorder.

The root samples/applies the backend once per fixed tick and runs a configurable pure substep loop. The prototype default is four substeps at the current `0.02 s` Unity fixed timestep. There is no giant vehicle controller.

## Assembly integration

A separate bounded M06 logical `AssemblyGraph` contains chassis, engine, starter, battery, fuel tank, clutch, gearbox, differential and four wheel representatives. `PrototypeAssemblyStateInitializer` establishes installed+tight state through the existing M05 save/restore boundary.

The adapter produces explicit failure flags and caches assembly queries by `GraphMutationCount`. It does not search scene names each frame.

The logical graph is not the moving physics body. `M06_DynamicProxyVehicle` is a separate graybox Rigidbody; logical part masses do not currently build the proxy mass or center of mass. This is an explicit M06 limitation and an M06A validation item.

## Powertrain behavior

Implemented prototype path:

```text
Starter -> Engine -> Clutch -> Gearbox -> Final drive -> Open differential -> configured driven-wheel pair
```

It supports start/crank/run/stall/off state, filtered throttle, idle correction, friction/inertia, torque curve and redline, clutch engagement/slip/capacity/temperature, neutral/reverse/five forward gears, invalid-shift reporting, equal open-differential split and wheel-speed feedback. Generic `LeftDrivenWheel` / `RightDrivenWheel` graph nodes are mapped by config indices; the authored asset uses FL/FR (`0/1`), so the current prototype is FWD. Root feedback and torque routing consume those indices, and focused EditMode coverage remaps the pair to RL/RR (`2/3`). One open-differential pair is supported; AWD is later. The present FWD selection is a project topology decision for the target vehicle; numeric dynamics remain provisional because the donor dynamic fixture is `Missing`.

Electrical voltage, fuel consumption and thermal state are simple placeholders with explicit prerequisites. Damage/wear and complete support networks are outside M06.

## Wheel backend and route

The backend exposes all required wheel sample/command fields through `IWheelPhysicsBackend` and uses non-allocating ray contacts, simple spring/damper load, provisional combined tire force, service braking and typed surface metadata.

Post-manual stability remediation initializes suspension history without a synthetic first-frame damper velocity, reports vehicle speed from gravity-plane velocity instead of full Rigidbody magnitude, and bounds passive tire force so a low-speed correction cannot reverse and amplify relative motion. Startup rest stabilization is deliberately level-spawn-only: it settles the unpowered proxy at reset, does not pin the vehicle on a six-degree slope, and disarms after settling so a later external `WakeUp()` plus `0.05 m/s` impulse is not put back to sleep. The prototype camera is detached from the Rigidbody and follows through a smoothed horizon-stabilized presentation component, so chassis heave/pitch no longer shakes the whole view.

The authored config selects FL/FR as driven and steered wheels; all four wheels receive up to the project-authored default `1800 N*m` service-brake command. This number remains `ProvisionalProjectTuning`, not donor evidence.

The generated route is a 100 m by 12 m bounded graybox track with four 25 m segments: paved, gravel, dirt and grass. It is a project-authored M06 test fixture, not donor or production-world parity. It does not close `WORLD-COL-003`; production collision-layer/PhysicMaterial classification remains open.

The builder appends `VehicleSimulationPrototype.unity` as build index/array position `9`, preserving Bootstrap `0` and production cells `6/8`.

## Reference values and provisional tuning

Reviewed exact/derived geometry used by M06:

- wheel anchors FL `(-0.6299995,0,1.1669996)`, FR `(0.6300007,0,1.1669996)`, RL `(-0.6029993,0,-1.167)`, RR `(0.603001,0,-1.1669996)` metres;
- wheelbase `2.334 m`;
- front/rear track `1.2600002 / 1.2060003 m`.

Interpretation guards:

- donor root `Rigidbody.mass = 389 kg` is not proven curb/assembled mass and is not the proxy mass;
- candidate radius `0.272667 m` remains `NeedsReview`/plausibility only because fitted-wheel identity is unproven;
- all dynamic fixtures are `Missing` in the reviewed donor dataset;
- all M06 dynamic values are `RemakeDesignTarget` / `ProvisionalProjectTuning`.

No donor executable, assembly, PlayMaker runtime, visual asset, texture or code is used by the simulation or build. The only bounded donor payload is the removable external Editor-audio diagnostic documented below; it is not simulation authority or serialized scene content.

## Telemetry and tools

Development overlay and fixed-capacity CSV recording cover powertrain, support state, prerequisites, all wheel/contact fields and tick/backend method timings. Editor tools under `Tools > MSC Remake > Vehicle Simulation` provide builder, validator, dashboard, graph/config inspection, calibration, reset, capture, prerequisite inspection and performance audit.

The recorder allocates/serializes only when capture stops; normal capture copies fixed value frames. CSV is local validation evidence.

## Tests and validation

Authored coverage:

- EditMode: `18` focused tests;
- PlayMode: `4` focused tests;
- static config/composition validator;
- calibration fixture;
- isolated performance audit;
- bounded manual drive checklist.

Fresh post-remediation/audio automated results were inspected on 2026-07-15 UTC (2026-07-16 local time):

- builder PASS: `M06_VEHICLE_SIMULATION_BUILD_OK`, version `1.1.0`, build index `9`, four wheels;
- strict config/composition validator PASS: `M06_VEHICLE_SIMULATION_VALIDATION_OK`;
- calibration PASS: `crankTicks=15`, `idleRpm=908.023`, label `ProvisionalProjectTuning`, donor dynamic fixture `Missing`;
- focused EditMode `18/18 PASS` in `0.1090665 s` and focused PlayMode `4/4 PASS` in `10.8416789 s`;
- all four focused PlayMode cases emitted `M06_LOCAL_DIAGNOSTIC_AUDIO_READY clips=7 source=ExternalDonorStaging hashes=Verified` after actual local load/hash verification;
- full EditMode `157/160 PASS` in `44.4875523 s`: all M06 tests pass; the three failures remain the known unrelated two M3 lighting assertions against the preserved user-owned `M3_NeutralVolume.asset` and one frozen 04A1 hash-drift assertion for `sharedassets3.assets/.resource`;
- full PlayMode `31/31 PASS`, zero failed/skipped, duration `49.4868179 s`; the ready marker appears in all four M06 cases;
- isolated performance audit PASS with zero measured allocations.

The first bounded manual run confirmed that start/run, gear selection, stall and RPM behavior worked at prototype level. It also reported about `5 km/h` startup creep and visible environment shake. Those two findings produced the bounded backend/camera remediation above; final focused PlayMode is `4/4 PASS`, including level rest, incline freedom and later external-wake coverage. On 2026-07-16 the user completed the post-remediation drive/audio recheck and confirmed that the basic prototype is good. Exact commands, filters, inspected result paths and the checklist are in `Docs/Vehicle/SIMULATION_TEST_MATRIX.md`.

## Local diagnostic Satsuma audio

The user authorized original vehicle sound only as an auditory diagnostic for this prototype. Seven frozen clips are read from external donor staging: three RPM layers (`850_idle5.ogg`, `850_mid3.ogg`, `850_mid13.ogg`), three starter events (`motor_start_1.ogg` through `motor_start_3.ogg`) and `starter_whine.ogg`. Static `GAME.unity` `AudioEngineSatsuma` references support the RPM-layer mapping; `MasterAudio/Starting` supports the starter mapping.

`Assets/Game/Audio/Runtime/VehicleAudioContracts.cs` owns the typed `IVehicleAudioBackend` parameter/event boundary. `Assets/Game/Vehicle/Runtime/VehicleAudioPresenter.cs` converts simulation telemetry into that boundary at `FixedUpdate`, and `Assets/Game/Audio/UnityFallback/UnityAudioBackend.cs` performs Editor-only hash verification, external loading and local mixing. PlayMode asserts the transition order `StarterEngaged -> StarterDisengaged -> EngineStarted`. Runtime reset is explicit: `VehicleSimulationHost.SimulationReset -> VehicleAudioPresenter -> VehicleAudioEvent.Reset`; resetting during `Cranking` is covered, and `UnityAudioBackend` handles the event with immediate `StopImmediately`. The builder composes one `M06_LocalDiagnosticVehicleAudio` child and the presenter; the strict validator rejects serialized `AudioSource`/`AudioClip` dependencies so donor payload cannot leak into the scene.

The clips are classified `TemporaryDirectImport`; the inspected routing evidence is `ReferenceOnly`. They remain outside Git and production `Assets`, are available only to the local Editor diagnostic path, and must not be included in a player build. The currently installed donor is mod-contaminated and has hash drift, so it is not used as the live source: the implementation is pinned to the frozen AssetRipper `1.3.14` staging export and the per-file SHA-256 values in `Docs/Porting/PORTING_LEDGER.csv`. If local staging/configuration is missing or invalid, the backend remains a silent fallback and automated simulation tests continue; a ready marker is required only when the configured files successfully load. No Satsuma-specific stop/stall clip has been proven; diagnostic loops fade out instead of claiming donor shutoff parity. Final vehicle audio remains newly authored or properly licensed behind `IAudioBackend`.

## Performance status

The normal simulation path uses preallocated wheel/command buffers, non-allocating raycasts and cached assembly/surface lookups. The authored audit measures:

- pure root cost at `1/2/4` substeps;
- allocations after warmup;
- direct prototype backend `Sample + Apply` method cost;
- fixed telemetry snapshot cost.

The durable output `Docs/Vehicle/M06_SIMULATION_PERFORMANCE.json` was freshly captured at `2026-07-15T19:37:34.5972700Z` with 512 warmup ticks, 10,000 measured pure ticks and 50,000 backend/telemetry iterations:

| Scope | Measured cost | Allocated bytes |
|---|---:|---:|
| Pure root, 1 substep | `2.85809 us/tick` | `0` |
| Pure root, 2 substeps | `4.09848 us/tick` | `0` |
| Pure root, 4 substeps | `6.49453 us/tick` | `0` |
| Backend `Sample + Apply` | `7.71657 us/iteration` | `0` |
| Telemetry buffer copy | `0.243964 us/iteration` | `0` |

The isolated audit therefore passes its allocation and bounded-method-cost evidence scope. Unity `Physics.Processing` is not isolated by that Stopwatch audit and was absent from the M05B.1 world capture, so **physics CPU remains unavailable until a later profiler/player capture**. This evidence is not a standalone 60 FPS claim.

## Principal files added or changed by M06

Runtime/model:

- `Assets/Game/Vehicle/Simulation/VehicleSimulationConfig.cs`;
- `Assets/Game/Vehicle/Simulation/VehicleSimulationContracts.cs`;
- `Assets/Game/Vehicle/Simulation/VehicleSimulationNodes.cs`;
- `Assets/Game/Vehicle/Simulation/VehicleSimulationRoot.cs`;
- `Assets/Game/Vehicle/Simulation/MSC.Vehicle.Simulation.asmdef`;
- `Assets/Game/Vehicle/Runtime/AssemblyVehiclePrerequisiteAdapter.cs`;
- `Assets/Game/Vehicle/Runtime/IVehicleInputSource.cs`;
- `Assets/Game/Vehicle/Runtime/PrototypeAssemblyStateInitializer.cs`;
- `Assets/Game/Vehicle/Runtime/PrototypeRaycastWheelPhysicsBackend.cs`;
- `Assets/Game/Vehicle/Runtime/ScriptedVehicleInputSource.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleSimulationHost.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleInputRouter.cs`;
- `Assets/Game/Vehicle/Runtime/VehiclePrototypeChaseCamera.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleAudioPresenter.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleResetController.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleSimulationPresenter.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleSurfaceMetadataAuthoring.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleTelemetryOverlay.cs`;
- `Assets/Game/Vehicle/Runtime/VehicleTelemetryRecorder.cs`;
- `Assets/Game/Vehicle/Runtime/MSC.Vehicle.Runtime.asmdef`.

Editor/content/tests:

- `Assets/Game/Editor/VehicleSimulation/`;
- `Assets/Game/Vehicle/Content/Simulation/Input/M06_Vehicle.inputactions`;
- generated `Assets/Game/Vehicle/Content/Simulation/Configurations`, `Assembly`, `Materials` and `Scenes` content after builder execution;
- `Assets/Game/Tests/EditMode/VehicleSimulation/`;
- `Assets/Game/Tests/PlayMode/VehicleSimulation/`;
- `Assets/Game/Audio/Runtime/VehicleAudioContracts.cs`;
- `Assets/Game/Audio/UnityFallback/UnityAudioBackend.cs`;
- the corresponding EditMode/PlayMode test asmdef references;
- `ProjectSettings/EditorBuildSettings.asset` for appended build index `9`.

Documentation:

- `Docs/Vehicle/SIMULATION_ARCHITECTURE.md`;
- `Docs/Vehicle/POWERTRAIN_GRAPH.md`;
- `Docs/Vehicle/WHEEL_BACKEND.md`;
- `Docs/Vehicle/SIMULATION_TUNING.md`;
- `Docs/Vehicle/TELEMETRY_GUIDE.md`;
- `Docs/Vehicle/SIMULATION_TEST_MATRIX.md`;
- `Docs/Vehicle/M06_SIMULATION_PERFORMANCE.json`;
- this report and strictly related architecture/roadmap/save/audio/performance/testing/porting updates.

## Manual acceptance

On 2026-07-16 the user completed the post-remediation/audio recheck in `VehicleSimulationPrototype.unity` and confirmed that everything is good for the basic prototype. This closes the bounded M06 manual gate covering the drive loop, startup rest/view remediation and local diagnostic audio. It does not claim donor handling/audio parity, production tuning or production-ready sound assets. No additional diagnostic capture was requested.

## Known limitations and risks

- Dynamic donor fixtures are missing; behavior is not parity-calibrated.
- Total mass, part contribution, center of mass, fitted wheel, ride height, clearance and suspension static state are unknown.
- Candidate radius and body mesh envelope are not proof of complete vehicle fit.
- Logical assembly and dynamic proxy state are separate.
- Prototype route is not a production route and does not exercise world streaming.
- Production surface policy `WORLD-COL-003` remains open.
- Advanced tire, damage/wear, complete electrical/fluid, save application, production audio and weather are not implemented.
- Donor Satsuma clips are temporary Editor-only diagnostics from frozen external staging; they are not final assets, build content or proof of complete donor audio behavior.
- Physics CPU evidence is unavailable.

## Readiness for M06A

Automated and manual readiness for `Prompts/06A_PHYSICS_VALIDATION.md` is **PASS**, with the three known unrelated full-EditMode failures explicitly retained rather than waived. The bounded M06 user acceptance was recorded on 2026-07-16.

The next and only next milestone is `Prompts/06A_PHYSICS_VALIDATION.md`. M06A has not begun, and no Milestone 07 work is authorized by this report.
