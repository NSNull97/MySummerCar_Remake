# Vehicle Simulation Tuning and Provenance

## Non-negotiable interpretation

All M06 dynamic tuning is classified:

- `VehicleReferenceClassification.RemakeDesignTarget`;
- label `ProvisionalProjectTuning`;
- source `ProjectAuthored/Milestone06`.

Reviewed donor dynamic fixtures are `Missing`. Current engine, clutch, gearbox, differential, tire, suspension, brake, electrical, fluid, thermal, acceleration and handling values must not be described as measured or observed donor behavior.

## Classification vocabulary

| Classification | Permitted meaning in M06/M06A |
|---|---|
| `MeasuredDonorReference` | A directly measured/hash-bound donor value with units and evidence |
| `ObservedDonorReference` | Repeatable donor behavior observed under recorded conditions |
| `DerivedReference` | A calculation from reviewed source measurements |
| `PlausibilityTarget` | Physically reasonable or candidate value, not donor truth |
| `RemakeDesignTarget` | Project-authored behavior/tuning target |
| `Unknown` | No defensible value yet |

`ProvisionalProjectTuning` is a status/label, not a donor-transfer classification.

## Available geometry evidence

| Item | Value | Classification | Use/boundary |
|---|---:|---|---|
| FL anchor | `(-0.6299995, 0, 1.1669996) m` | `DerivedReference` | M06 anchor |
| FR anchor | `(0.6300007, 0, 1.1669996) m` | `DerivedReference` | M06 anchor |
| RL anchor | `(-0.6029993, 0, -1.167) m` | `DerivedReference` | M06 anchor |
| RR anchor | `(0.603001, 0, -1.1669996) m` | `DerivedReference` | M06 anchor |
| Wheelbase | `2.334 m` | `DerivedReference` | exact derivation from reviewed roots |
| Front track | `1.2600002 m` | `DerivedReference` | exact derivation from reviewed roots |
| Rear track | `1.2060003 m` | `DerivedReference` | exact derivation from reviewed roots |
| `datsun_body` AABB W/H/L | `1.465916 / 1.1102937 / 3.5462036 m` | measured body-mesh reference | body mesh only, not assembled envelope |
| Donor root Rigidbody mass | `389 kg` | serialized donor component value | not curb/assembled mass; not M06 proxy mass |
| Candidate `tire_stock` radius | `0.272667 m` | `PlausibilityTarget`, `NeedsReview` | unproven fitted tire; provisional use only |

## Missing donor dynamic fixtures

The reference database does not currently provide validated donor targets for:

- total curb/assembled mass, part mass contribution or center of mass;
- fitted wheel/tire identity, radius, inertia or pressure;
- engine torque curve, inertia, friction, starter time, idle stability, throttle/free-rev response or redline;
- clutch capacity/curve/slip heat, gear ratios, final drive or efficiency;
- steering lock/ratio/turning radius;
- spring/damper rates, travel, static compression, ride height or ground clearance;
- tire stiffness/friction, surface response, brake torque/balance/distance;
- acceleration, coast-down, top speed, lateral response or bump behavior;
- battery, fuel, oil, coolant and thermal network values.

M06A must keep those targets `Unknown`, `PlausibilityTarget` or `RemakeDesignTarget` until new evidence is captured and reviewed.

## Automated calibration fixture result

The fresh Unity calibration fixture passed on 2026-07-15:

```text
M06_VEHICLE_SIMULATION_CALIBRATION_OK label=ProvisionalProjectTuning crankTicks=15 idleRpm=908.023 referenceDynamicFixture=Missing provisionalOnly=true
```

This proves that the current project-authored fixture cranks and settles within its own bounded assertions. It is not donor parity evidence: `referenceDynamicFixture=Missing` and `provisionalOnly=true` remain authoritative.

## Prototype configuration

Configuration asset target: `Assets/Game/Vehicle/Content/Simulation/Configurations/M06_VehicleSimulationConfig.asset`.

### Tick and engine

| Parameter | Value |
|---|---:|
| Fixed timestep (project setting) | `0.02 s` |
| Pure substeps | `4` |
| Engine inertia | `0.24 kg*m^2` |
| Idle target | `900 rpm` |
| Start threshold | `450 rpm` |
| Stall threshold | `500 rpm` |
| Redline | `7000 rpm` |
| Maximum RPM | `7400 rpm` |
| Base friction | `8 N*m` |
| Viscous friction | `0.02 N*m per rad/s` |
| Engine braking | `18 N*m` |
| Throttle response | `5 s^-1` |
| Starter torque | `65 N*m` |
| Starter maximum | `650 rpm` |
| Stall delay | `0.28 s` |

Prototype torque samples are `(rpm, N*m)`: `(0,0)`, `(700,68)`, `(1800,96)`, `(3500,108)`, `(5200,92)`, `(7000,0)`.

### Clutch and gearbox

| Parameter | Value |
|---|---:|
| Clutch maximum torque | `220 N*m` |
| Slip stiffness | `7.5` |
| Engagement exponent | `1.6` |
| Reverse | `-3.25` |
| Forward ratios | `3.50, 2.05, 1.36, 1.00, 0.82` |
| Final drive | `3.90` |
| Efficiency | `0.90` |

### Chassis, wheel and suspension

| Parameter | Value |
|---|---:|
| Proxy mass | `650 kg` |
| Wheel radius | `0.272667 m` |
| Wheel inertia | `1.15 kg*m^2` |
| Left/right driven-wheel indices | `0 / 1` (authored FWD config; one configurable distinct pair) |
| Maximum service brake torque, per wheel command | `1800 N*m` |
| Maximum handbrake torque (currently unwired) | `850 N*m` |
| Low-speed steering angle | `30 deg` |
| High-speed steering angle | `12 deg` |
| Steering fade speed | `25 m/s` |
| Suspension rest length | `0.32 m` |
| Suspension travel | `0.18 m` |
| Spring rate | `28000 N/m` |
| Damper rate | `3500 N*s/m` |
| Longitudinal stiffness | `7000` |
| Lateral stiffness | `6200` |
| Base rolling-resistance coefficient | `0.015` |

### Electrical, fluid and thermal placeholders

| Parameter | Value |
|---|---:|
| Nominal/minimum crank voltage | `12.6 / 9.5 V` |
| Starter drain / alternator recovery | `0.16 / 0.04 V/s` |
| Initial/minimum fuel | `15 / 0.05 L` |
| Initial/minimum oil | `3.5 / 1 L` |
| Initial/minimum coolant | `5 / 1 L` |
| Idle/full-load fuel rate | `0.00035 / 0.0025 L/s` |
| Ambient/operating temperature | `20 / 88 deg C` |
| Heating/cooling rates | `0.035 / 0.012 s^-1` |

Surface multipliers are listed in `WHEEL_BACKEND.md`.

`LeftDrivenWheelIndex` / `RightDrivenWheelIndex` control root wheel-speed feedback and torque routing. The current asset selects FL/FR (`0/1`); focused validation also remaps the pair to RL/RR (`2/3`). This does not implement AWD or multiple differential branches.

## Logical fixture values are not physics calibration

M06 part-definition masses and proxy shapes exist to exercise assembly prerequisites. They do not sum into the 650 kg Rigidbody and are not validated donor part masses. The dynamic proxy likewise does not prove center of mass, curb state or final collider dimensions.

## Safe tuning workflow

Until M06A:

1. modify only the central `VehicleSimulationConfig` asset/default builder values;
2. preserve `RemakeDesignTarget` / `ProvisionalProjectTuning` labels;
3. record the reason and affected fixture;
4. run config validation, focused tests, calibration fixture and performance audit;
5. compare repeated telemetry rather than one subjective drive;
6. do not change the camera, timescale or world geometry to hide a physics defect.

M06A must create a versioned tuning change log with before/after metrics. It must not promote the candidate tire radius or 389 kg component mass to curb/fitted truth without new evidence.
