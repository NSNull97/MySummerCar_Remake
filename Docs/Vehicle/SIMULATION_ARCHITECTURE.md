# Vehicle Simulation Architecture — Milestone 06

## Scope and status

Milestone 06 introduces a bounded, clean-room vehicle-simulation foundation. It is a prototype for start, idle, stall, clutch, gearbox, a configurable single driven-wheel pair, steering, braking, suspension contact, support-system prerequisites and telemetry. The current authored config maps that pair to FL/FR, so the prototype is FWD. It is not final vehicle realism or donor-calibrated behavior.

Fresh automated Unity validation passes; the bounded manual drive smoke test remains `Pending user execution`. Exact evidence and the distinction between automated and manual gates are recorded in `SIMULATION_TEST_MATRIX.md` and `MILESTONE_06_REPORT.md`.

## Assembly boundaries

| Assembly | Owns | May depend on |
|---|---|---|
| `MSC.Vehicle.Simulation` | Config, state, pure simulation nodes, powertrain graph, prerequisite and wheel-backend contracts, telemetry | Unity engine value types only; no project runtime assembly |
| `MSC.Vehicle.Assembly` | Part/mount/fastener graph and save DTOs | Existing M05 dependencies |
| `MSC.Vehicle.Runtime` | Unity composition host, assembly adapter, input, PhysX backend, reset, presentation and telemetry recorder | `MSC.Vehicle.Simulation`, `MSC.Vehicle.Assembly`, Input System |
| `MSC.Editor` | M06 builder, validator, calibration, inspection and performance tools | Runtime modules; Editor only |

No runtime assembly references an Editor assembly. The simulation layer does not search scenes, query donor data or own presentation GameObjects.

## Runtime composition

The prototype composition is explicit:

```text
VehicleInputRouter (Update snapshot and latched edges)
    |
    v
VehicleSimulationHost (FixedUpdate composition root)
    |---- AssemblyVehiclePrerequisiteAdapter ----> M06 logical AssemblyGraph
    |---- VehicleSimulationRoot -----------------> pure simulation state
    |---- IWheelPhysicsBackend ------------------> dynamic proxy Rigidbody / PhysX
    |
    +---- VehicleSimulationPresenter (LateUpdate, wheel visuals only)
    +---- VehicleTelemetryOverlay (development presentation)
    +---- VehicleTelemetryRecorder (development capture)
```

`VehicleSimulationHost` is intentionally thin. It consumes one input snapshot, invokes one fixed simulation tick and exposes state/telemetry. It does not implement the engine, gearbox, tire model or assembly rules.

## Fixed-step policy

The current Unity fixed timestep is `0.02 s`. The exact flow for each `FixedUpdate` is:

1. `VehicleInputRouter` supplies the latest continuous controls and consumes latched gear/reset edges.
2. `IWheelPhysicsBackend.Sample` runs once and fills a preallocated four-wheel sample buffer.
3. The selected gear is accepted or rejected explicitly.
4. `AssemblyVehiclePrerequisiteAdapter` supplies a cached assembly snapshot; support-system and input prerequisites are added.
5. `VehicleSimulationRoot` runs `VehicleSimulationConfig.SubstepCount` pure substeps; the prototype default is `4`.
6. Substep wheel commands are averaged.
7. `IWheelPhysicsBackend.Apply` runs once, so simulation substeps do not multiply PhysX forces.
8. Finite-state validation and telemetry update complete the tick.
9. `VehicleSimulationPresenter` updates wheel presentation in `LateUpdate`.

Changing the render frame rate does not change the simulation delta. Tests use tolerances; the project does not claim bitwise determinism across PhysX versions, hardware or platforms.

## Simulation ownership

`VehicleSimulationRoot` owns one `VehicleSimulationState` and coordinates small nodes:

- `StarterSimulation` and `EngineSimulation`;
- `ClutchSimulation`;
- `GearboxSimulation` and `DifferentialSimulation`;
- `BrakeSimulation`, `SteeringSimulation`, `SuspensionSimulation` and `WheelSimulation`;
- `ElectricalSimulation`, `FluidSimulation` and `ThermalSimulation`.

`PowertrainGraph` exposes the single-pair prototype topology through generic `LeftDrivenWheel` and `RightDrivenWheel` nodes. `VehicleSimulationConfig.LeftDrivenWheelIndex` / `RightDrivenWheelIndex` select two distinct indices from the four-wheel array; the current authored asset uses FL/FR (`0/1`), so its present layout is FWD. Root wheel-speed feedback and `WheelSimulation` torque routing read those indices instead of hard-coding an axle, and EditMode coverage also switches the pair to RL/RR (`2/3`). One open-differential pair is supported; AWD/multiple driven pairs remain a future extension. Numeric torque/traction values remain `ProvisionalProjectTuning`; the reviewed donor dynamic fixture is still `Missing`. Algorithms remain individual classes instead of a monolithic car controller.

## Assembly integration and the dynamic proxy

The M05 representative graph is not silently treated as a complete drivable car. Milestone 06 authors its own bounded logical fixture with these project-owned definition IDs:

```text
m06.chassis
m06.engine
m06.starter
m06.battery
m06.fuel_tank
m06.clutch
m06.gearbox
m06.differential
m06.wheel.fl
m06.wheel.fr
m06.wheel.rl
m06.wheel.rr
```

`PrototypeAssemblyStateInitializer` uses the existing assembly capture/restore boundary to make the fixture installed and tightened. `AssemblyVehiclePrerequisiteAdapter` then checks representative part paths and caches its failure flags by `GraphMutationCount`; it does not allocate or rescan the graph every simulation tick when the graph is unchanged.

The logical fixture and the moving physics proxy are deliberately separate:

- the logical `AssemblyGraph` is authority for installed/secured prerequisites;
- `M06_DynamicProxyVehicle` is one project-authored graybox `Rigidbody` used by the prototype backend;
- logical part masses and transforms do not automatically build the current Rigidbody mass or center of mass;
- no logical `PartInstance.Body` points at the proxy chassis.

This separation is temporary but explicit. Milestone 06A must define the validated bridge from installed parts to physical mass/center-of-mass state instead of assuming the two graphs are already equivalent.

## Prerequisite results

Missing state is represented by `VehicleSimulationPrerequisiteFailure` flags, including missing engine/starter/battery/fuel/drivetrain/wheels, unsecured wheels, ignition, battery voltage, oil, coolant, neutral, clutch disengagement and invalid input/gear state.

The engine may crank only when crank prerequisites pass, may run only when run prerequisites pass, and may transmit torque only when the drivetrain, gear and clutch path also passes. Missing references produce explicit initialization or prerequisite failures rather than a `NullReferenceException` or hidden fallback.

## State and persistence boundary

`VehicleSimulationState` contains engine, clutch, gearbox, differential, vehicle, electrical, fluid, thermal and four-wheel state. `CaptureDto` and `TryRestoreDto` expose schema `1` through `VehicleSimulationStateDto`.

This is a domain serialization fixture only. It does not write a save slot, resolve a stable vehicle ID, migrate a full save document or restore a Rigidbody pose. Integration with the project save root remains future work.

## Telemetry and performance boundary

`VehicleTelemetry` is updated after each fixed tick and contains simulation/backend timings plus powertrain and wheel state. `VehicleSimulationHost` wraps the tick with profiler marker `MSC.Vehicle.Simulation.FixedTick`.

The development performance audit can measure isolated pure simulation, direct backend method cost and telemetry-buffer copy cost. It explicitly cannot isolate Unity `Physics.Processing`; physics CPU remains unavailable until a later profiler/player capture.

## Failure policy

- Invalid config or composition: initialization fails with an actionable message.
- Backend wheel count mismatch: constructor fails.
- Non-finite input: neutralized and flagged.
- Non-finite backend speed or simulation state: explicit exception.
- Invalid shift: state remains valid, shift is rejected and counted.
- Reset: zero proxy velocities, restore the configured pose, reset backend and simulation state.

## Current limitations

- All dynamic constants are `RemakeDesignTarget` / `ProvisionalProjectTuning`; donor dynamic fixtures are `Missing`.
- The 650 kg proxy mass is not donor curb/assembled mass.
- The logical fixture and proxy mass/center-of-mass are not coupled.
- Damage, wear, complete fluid/electrical networks, advanced tire forces, anti-roll, Ackermann steering and limited-slip behavior are not implemented.
- The 100 m graybox surface track is a bounded M06 fixture, not production-world road parity.
- Production surface/layer/PhysicMaterial policy issue `WORLD-COL-003` remains open.
- Save, audio, weather and final presentation integration are intentionally outside M06.

The only next validation milestone after a successful M06 gate is `Prompts/06A_PHYSICS_VALIDATION.md`.
