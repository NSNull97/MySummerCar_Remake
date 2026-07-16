# Milestone 06 Powertrain Graph

## Explicit topology

The prototype graph is inspectable through `PowertrainGraph` and the Editor command `Tools > MSC Remake > Vehicle Simulation > Open Powertrain Graph`.

```text
                        driven-wheel speed feedback
                  +-------------------------------------+
                  |                                     |
Engine -> Clutch -> Gearbox -> Final drive -> Open differential
                                                   |            |
                                                   v            v
                                           Left driven wheel Right driven wheel
```

The graph contains seven named nodes: `Engine`, `Clutch`, `Gearbox`, `FinalDrive`, `OpenDifferential`, `LeftDrivenWheel` and `RightDrivenWheel`. The final two names describe differential sides, not fixed physical corners.

`VehicleSimulationConfig.LeftDrivenWheelIndex` and `RightDrivenWheelIndex` map the single differential pair into the four-wheel array. The current authored config maps it to FL/FR (indices `0/1`), producing FWD; a focused EditMode case remaps it to RL/RR (`2/3`) and verifies torque routing. AWD or multiple driven pairs are not implemented. The current FWD choice is a project topology decision for the target vehicle, not an inference from the `Missing` donor dynamic fixture. Ratios, torque transfer, grip and other numeric dynamics remain `RemakeDesignTarget` / `ProvisionalProjectTuning`.

## Node contracts

| Node | Inputs | State/output | Current rule |
|---|---|---|---|
| Starter | request, crank prerequisites, engine RPM | starter torque | Torque is available below starter maximum RPM only |
| Engine | throttle, starter torque, clutch load, ignition/run prerequisites, delta | status, RPM, torque, load, filtered throttle, stall timer | Inertia integration with friction, idle correction, redline cutoff and delayed stall |
| Clutch | engine and gearbox-input angular speed, pedal | engagement, slip, transferred torque, temperature | Capacity-limited slip stiffness; pedal `1` is disengaged |
| Gearbox | requested gear, clutch torque | selected gear, input/output speed, output torque | Neutral, reverse and five forward ratios; invalid request rejected |
| Final drive | gearbox output | multiplied torque | Ratio is part of `GearboxSimulationConfig` |
| Open differential | final-drive torque | left/right torque | Equal `50/50` split |
| Driven-wheel pair | per-side torque, steering/brake inputs, configured wheel indices | wheel command and speed feedback | One configurable left/right pair; backend owns contact and force application |

## Prototype ratios

All ratios are `RemakeDesignTarget` / `ProvisionalProjectTuning`, not donor measurements.

| Selection | Ratio |
|---|---:|
| Reverse | `-3.25` |
| Neutral | `0` |
| 1 | `3.50` |
| 2 | `2.05` |
| 3 | `1.36` |
| 4 | `1.00` |
| 5 | `0.82` |
| Final drive | `3.90` |
| Efficiency | `0.90` |

For a non-neutral gear, the current torque path is:

```text
gearbox output torque = clutch torque * selected ratio * final-drive ratio * efficiency
each configured driven-wheel torque = gearbox output torque * 0.5
```

The current gearbox-input speed is derived from the mean angular speed of the two configured driven wheels multiplied by selected ratio and final drive. No physical gear teeth, shaft compliance or synchronization are simulated.

## Engine state transitions

```text
Off -- starter + crank prerequisites --> Cranking
Cranking -- RPM >= start threshold + run prerequisites --> Running
Cranking -- starter released before start --> Stalled
Running -- RPM below stall threshold for stall delay --> Stalled
Running/Cranking/Stalled -- ignition off --> Off
```

Prototype thresholds are documented in `SIMULATION_TUNING.md`. The engine can be running in neutral, but torque transmission additionally requires a valid non-neutral gear, an engaged clutch and a complete/secured assembly path.

## Shift behavior

`VehicleShiftStatus` is one of:

- `Stable` when no shift edge is consumed;
- `AcceptedThisTick` for a valid selection;
- `RejectedThisTick` for an unsupported gear.

An invalid request does not corrupt the selected gear. It increments `InvalidShiftCount` and contributes `InvalidSelectedGear` to the prerequisite result for that tick.

## Assembly prerequisite path

The bounded logical assembly requires installed and secured representatives for:

- engine;
- starter;
- battery;
- fuel tank;
- clutch, gearbox and differential;
- all four wheels, with required fasteners tightened.

The current authored config selects FL/FR (indices `0/1`), while the simulation topology permits any two distinct wheel indices as the single left/right driven pair. All four wheel parts are nevertheless required because a drivable fixture must have a complete wheel set. The adapter caches its result until `AssemblyGraph.GraphMutationCount` changes.

## Support systems

Electrical, fluid and thermal nodes are deliberately simple:

- starter activity drains voltage and a running engine restores it toward nominal voltage;
- running fuel use interpolates between idle and full-load placeholder rates;
- oil and coolant are prerequisites but are not consumed in M06;
- engine/coolant temperature moves toward a load-adjusted operating target.

These nodes provide state and future extension points; they are not a complete vehicle network.

## Known limitations

- No measured donor torque curve, ratios, final drive, starter time, idle response, acceleration or stall fixture exists in the reviewed dataset.
- Clutch wear and damage hooks are not yet persisted; temperature is a simple slip-energy proxy.
- `BrakeSimulation` exposes a handbrake calculation, but the M06 input/command path supplies service brake only.
- The open differential has no traction-limited torque redistribution or limited-slip extension yet.
- Only one driven pair is represented; AWD/multiple differential branches are future work.
- Engine temperature does not currently derate torque.
- RPM/speed calibration belongs to M06A and must use typed reference classifications.

Do not relabel current results as donor parity. They are sufficient only to establish the separated end-to-end prototype path.
