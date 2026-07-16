# Vehicle Simulation Telemetry Guide

## Purpose

M06 telemetry is a development diagnostic boundary. It helps reproduce simulation behavior and performance; it is not player UI, a save format or proof of donor parity.

## Live overlay

`VehicleTelemetryOverlay` is present only in the Editor or a Development Build. It displays:

- RPM, filtered throttle, engine load and engine torque;
- selected gear and shift status;
- clutch slip;
- gearbox input/output speed and differential torque;
- vehicle speed, steering and brake input;
- battery voltage and engine temperature;
- explicit prerequisite flags;
- fixed-step, substep count and measured tick/backend method timings;
- per-wheel contact, normal load, longitudinal/lateral slip, angular speed, suspension compression and surface type.

The overlay is presentation-only and does not drive simulation state.

## Recorder

`VehicleTelemetryRecorder` stores fixed-size value frames during `FixedUpdate` and formats/writes CSV only when capture stops.

Prototype defaults:

- capacity: `18000` frames;
- at `0.02 s` fixed timestep: approximately `360 s` / 6 minutes;
- path: `VehicleTelemetry/Milestone06/VehicleTelemetry.csv`, resolved from the process working directory;
- encoding: UTF-8 without BOM;
- numeric format: invariant culture, round-trip representation;
- full buffer: capture stops and sets `CapacityReached`; it does not overwrite old frames.

Raw telemetry is local validation evidence. Do not commit it to the porting ledger or Git as donor/reference payload. Promote only reviewed, compact project-owned summaries if a later validation milestone requires durable evidence.

## Capture workflow

1. Build or open `Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity`.
2. Enter Play Mode.
3. Use `Tools > MSC Remake > Vehicle Simulation > Start/Stop Telemetry Capture` to start.
4. Execute one clearly defined scenario; record controls, config, timestep/substeps and expected surface.
5. Invoke the same command to stop and export.
6. Inspect the Console marker for frame count, capacity state and absolute output path.
7. Keep trials separate rather than mixing start, acceleration, braking and reset into an undocumented file.

The Dashboard exposes the same command. Calling capture outside Play Mode fails explicitly.

## CSV schema

Global columns:

```text
elapsed_s
rpm
throttle
engine_load
engine_torque_nm
clutch_slip_rpm
gear
shift_status
gearbox_input_rad_s
gearbox_output_rad_s
differential_torque_nm
speed_m_s
steering_deg
brake
battery_v
engine_temp_c
prerequisite_flags
fixed_step_s
substeps
simulation_ms
backend_sample_ms
backend_apply_ms
```

For each `w0` through `w3` the file appends:

```text
contact
normal_load_n
longitudinal_slip
lateral_slip
omega_rad_s
suspension_01
surface
```

Wheel order is FL, FR, RL, RR. Enum/flag columns are serialized as integer values; interpret them against `VehicleShiftStatus`, `VehicleSurfaceType` and `VehicleSimulationPrerequisiteFailure` from the same source revision.

## Timing interpretation

- `simulation_ms` is Stopwatch wall time from the beginning of `VehicleSimulationRoot.Tick` through backend apply and finite-state validation.
- `backend_sample_ms` and `backend_apply_ms` time only direct method calls.
- These values do not isolate the later Unity `Physics.Processing` phase.
- They are diagnostic per-tick samples, not a substitute for Unity Profiler/player captures.
- Physics CPU is unavailable until a later capture records the proper profiler counter.

The M05B.1 world capture has valid bounded frame/GPU evidence without M06 physics, but it also lacks an isolated physics counter. Do not subtract the two captures and call the result vehicle physics cost.

## Recommended M06A scenarios

Use a fresh file and stable scripted/manual procedure for each:

- start and idle;
- free-rev/throttle step;
- launch and acceleration interval;
- coast-down;
- braking interval;
- steering/slalom;
- bump/suspension response;
- paved/gravel/dirt/grass comparison;
- reset/recovery;
- production-world/streaming-boundary drive once such a route is validated.

Record repeated trials where PhysX variance matters. Preserve classification of every comparison target; current M06 dynamic tuning is `RemakeDesignTarget` / `ProvisionalProjectTuning` because donor dynamic fixtures are `Missing`.

## Failure cues

- Non-zero prerequisite flags: the requested action is blocked or the fixture is incomplete.
- `RejectedThisTick`: invalid gear request.
- Non-finite backend/simulation state: runtime throws; treat as a failing scenario.
- `CapacityReached = true`: repeat with a shorter scenario or deliberately larger reviewed buffer.
- Unknown surface: contact collider lacks `VehicleSurfaceMetadataAuthoring`; this is not permission to infer from a texture/material name.
