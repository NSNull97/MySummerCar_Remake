/plan

# MILESTONE 06 — VEHICLE SIMULATION PROTOTYPE

Read `AGENTS.md` completely before doing anything.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md` when present.

Read:

- all reports through Milestone 05B;
- `Docs/VEHICLE_SYSTEM.md`;
- vehicle-assembly architecture and tests;
- reference-capture database and calibration fixtures;
- world-validation and road/collision reports;
- save, audio, performance, and testing documentation;
- current Git status and diff.

## Player-representation boundary

Do not introduce a physical full-body player, world-space hand IK, or animation-
dependent interaction. Assembly, controls and vehicle entry remain gameplay-
driven; optional first-person viewmodel arms are presentation only.

## Objective

Implement a separated, testable, data-driven vehicle simulation prototype that
works with the assembled vehicle state.

The prototype must support:

- engine start, idle, throttle, load, stall, and shutdown;
- clutch coupling and slip;
- gearbox ratios and neutral;
- final drive/differential;
- wheel/brake force path;
- steering input;
- basic suspension and tire/contact backend;
- starter, battery, and minimum electrical prerequisites;
- simplified thermal and fluid prerequisites;
- telemetry;
- a short drivable vertical-slice route.

This milestone is a simulation foundation, not final realism tuning.

## Architecture

Keep simulation separate from Unity presentation and physical authoring.

Create or align:

- `VehicleSimulationRoot`;
- `VehicleSimulationConfig`;
- `VehicleSimulationState`;
- `VehicleInputState`;
- `VehicleTelemetry`;
- `PowertrainGraph`;
- `EngineSimulation`;
- `StarterSimulation`;
- `ClutchSimulation`;
- `GearboxSimulation`;
- `DifferentialSimulation`;
- `BrakeSimulation`;
- `SteeringSimulation`;
- `WheelSimulation`;
- `SuspensionSimulation`;
- `ThermalSimulation`;
- `FluidSimulation`;
- `ElectricalSimulation`;
- `VehicleSimulationPrerequisites`;
- `VehicleSimulationPresenter`;
- `IWheelPhysicsBackend`;
- one simple prototype backend;
- calibration fixtures and validators.

Do not create one giant car controller.

## Assembly integration

Simulation availability must derive from assembly state.

At minimum validate:

- engine-related representative parts are present;
- starter/battery prerequisites;
- fuel availability;
- required drivetrain path;
- wheel installation and fastening;
- selected gear;
- clutch state;
- ignition state.

Use explicit adapters/queries from the assembly graph.

Do not let simulation search scene objects by name every frame.

A missing part must produce an explicit simulation prerequisite result rather
than a NullReferenceException.

## Time stepping

Define an explicit simulation tick policy.

- Use fixed-step simulation.
- Make substep count configurable.
- Separate input sampling, simulation, and presentation.
- Avoid frame-rate-dependent formulas.
- Do not claim cross-platform bitwise determinism.
- Use numeric tolerances in tests.
- Record the relationship to Unity `FixedUpdate` and PhysX.

## Engine prototype

Support configurable:

- inertia;
- idle target;
- starter torque;
- friction;
- throttle response;
- torque curve;
- redline;
- stall RPM/conditions;
- engine braking;
- load;
- fuel-use placeholder;
- temperature influence placeholder;
- damage/wear hooks.

Use reference fixtures where available.

Unknown values must be marked as provisional tuning, not donor measurements.

## Clutch and gearbox

Support:

- clutch pedal/input;
- engagement curve;
- transferable torque;
- slip;
- heat/wear hooks;
- neutral;
- forward gears;
- reverse;
- gear ratios;
- shift state;
- invalid shift handling;
- final-drive ratio.

Do not simulate gear teeth physically.

## Differential and driven wheels

Support:

- configurable driven axle/wheels;
- open differential prototype;
- torque distribution;
- wheel-speed feedback;
- future limited-slip backend extension.

Keep the powertrain graph explicit and inspectable.

## Wheels, tires, suspension, and brakes

Provide a simple end-to-end backend sufficient for a drivable prototype.

It must expose:

- wheel contact;
- normal load;
- longitudinal slip;
- lateral slip;
- wheel angular speed;
- drive torque;
- brake torque;
- steering angle;
- suspension compression;
- surface metadata.

The backend may initially wrap a simple implementation, but all simulation code
must depend on `IWheelPhysicsBackend`.

Do not bury the entire powertrain inside `WheelCollider`.

Support future replacement with a custom tire-force backend.

## Surface integration

Read road/world surface metadata where available:

- paved;
- gravel;
- dirt;
- grass;
- mud/wet placeholder.

Provide configurable friction/rolling-resistance responses.

Do not hard-code terrain texture indices in simulation code.

## Telemetry and debug tools

Create a development-only telemetry overlay and recorder for:

- RPM;
- throttle;
- engine load;
- torque;
- clutch slip;
- selected gear;
- gearbox input/output speed;
- differential torque;
- wheel speed/slip;
- vehicle speed;
- steering;
- braking;
- suspension compression;
- battery voltage;
- temperatures;
- simulation prerequisites;
- fixed-step/substep timing.

Allow export to CSV or another project-approved format.

Create tools under:

`Tools → MSC Remake → Vehicle Simulation`

Include:

- validate configs;
- open powertrain graph;
- run calibration fixture;
- spawn/reset prototype vehicle;
- start/stop telemetry capture;
- compare reference/tuned curves;
- show missing prerequisites.

## Prototype scene

Create or update a bounded prototype that demonstrates:

1. assembled vehicle prerequisites;
2. engine start;
3. idle;
4. gear selection;
5. clutch engagement;
6. moving from rest;
7. steering;
8. braking;
9. stall;
10. reset/recovery.

Use an approved production or prototype world route.

Do not create unrelated world art.

## Tests

Add EditMode tests for:

- torque interpolation;
- friction/inertia behavior;
- idle controller;
- starter prerequisites;
- start/stall transitions;
- clutch torque transfer;
- clutch slip;
- gear ratio mapping;
- neutral;
- reverse;
- differential torque distribution;
- brake torque;
- prerequisite queries;
- fixed-step independence within tolerance;
- simulation-state serialization fixture;
- telemetry calculations.

Add PlayMode tests where practical for:

- start and idle;
- failed start with missing prerequisite;
- move from rest;
- shift sequence;
- brake to stop;
- stall;
- wheel contact;
- basic road-surface change;
- no NaN/infinite state;
- no explosive physics on load/reset.

## Performance

Measure:

- simulation CPU time;
- physics CPU time;
- substep cost;
- allocations;
- telemetry overhead;
- wheel-backend cost.

No per-tick allocations in normal simulation paths.

Do not optimize by obscuring correctness before profiling.

## Non-goals

Do not implement:

- final advanced tire model;
- final damage/wear;
- complete fluid/electrical network;
- final sound design;
- final weather influence;
- multiplayer prediction;
- full AI driving;
- final balancing for every vehicle;
- cinematic camera systems.

## Documentation

Create or update:

- `Docs/Vehicle/SIMULATION_ARCHITECTURE.md`;
- `Docs/Vehicle/POWERTRAIN_GRAPH.md`;
- `Docs/Vehicle/WHEEL_BACKEND.md`;
- `Docs/Vehicle/SIMULATION_TUNING.md`;
- `Docs/Vehicle/TELEMETRY_GUIDE.md`;
- `Docs/Vehicle/SIMULATION_TEST_MATRIX.md`;
- `Docs/Milestones/MILESTONE_06_REPORT.md`;
- roadmap and architecture docs.

## Definition of done

1. The simulation compiles.
2. It consumes assembly state.
3. Engine/clutch/gearbox/differential path works.
4. A prototype vehicle can start, move, steer, brake, and stall.
5. Telemetry exists.
6. Tests exist and run when possible.
7. Values are labeled measured versus provisional.
8. No giant controller was created.
9. The report lists readiness for physics validation.

## Final response

Report:

1. Architecture implemented.
2. Assembly integration.
3. Powertrain behavior.
4. Wheel backend.
5. Prototype route/results.
6. Reference values and provisional tuning.
7. Tests.
8. Performance.
9. Files changed.
10. Manual steps.
11. Known limitations.
12. Readiness for `06A_PHYSICS_VALIDATION.md`.

Stop after Milestone 06.
