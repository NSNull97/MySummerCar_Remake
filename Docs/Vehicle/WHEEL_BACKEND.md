# Wheel Physics Backend — Milestone 06

## Contract

Simulation depends on `IWheelPhysicsBackend`, not on a concrete Unity wheel component.

```csharp
int WheelCount { get; }
float VehicleSpeedMetersPerSecond { get; }
void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination);
void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands);
void Reset();
```

Each sample exposes contact, point/normal, normal load, longitudinal and lateral slip, angular speed, suspension compression and typed surface metadata. Each command exposes drive torque, brake torque and steering angle.

The caller owns fixed-size buffers. `Sample` and `Apply` must not retain or resize them during a normal tick.

## Prototype implementation

`PrototypeRaycastWheelPhysicsBackend` is a bounded four-wheel raycast backend for one dynamic graybox Rigidbody. It is not a `WheelCollider` wrapper and does not contain the powertrain.

Per fixed tick it:

1. samples one non-allocating raycast per wheel with a fixed 16-hit buffer;
2. selects the nearest hit not belonging to the chassis hierarchy;
3. computes suspension compression and spring/damper normal load;
4. derives longitudinal/lateral contact velocities and slip;
5. resolves a typed `VehicleSurfaceMetadataAuthoring` provider from the collider hierarchy and caches it while the collider is unchanged;
6. clamps the combined tire-force vector to `normal load * surface friction multiplier`;
7. applies suspension and tire force at the contact point;
8. integrates bounded wheel angular speed and spin presentation.

Pure simulation substeps produce an averaged command. PhysX sampling and force application each occur once per Unity fixed tick.

At full service-brake input the prototype commands `1800 N*m` to each wheel. This is `ProvisionalProjectTuning`, not a donor braking measurement.

## Wheel ordering and reviewed geometry

The required order is front-left, front-right, rear-left, rear-right.

| Index | ID | Local anchor in metres | Role |
|---:|---|---|---|
| 0 | `m06.wheel.fl` | `(-0.6299995, 0, 1.1669996)` | current authored driven-left, steered, service brake |
| 1 | `m06.wheel.fr` | `(0.6300007, 0, 1.1669996)` | current authored driven-right, steered, service brake |
| 2 | `m06.wheel.rl` | `(-0.6029993, 0, -1.167)` | service brake |
| 3 | `m06.wheel.rr` | `(0.603001, 0, -1.1669996)` | service brake |

These anchors are `DerivedReference` from the reviewed `04B.4VehicleWheelAnchors` geometry record. Derived dimensions are:

- wheelbase: `2.334 m`;
- front track: `1.2600002 m`;
- rear track: `1.2060003 m`.

The validator requires each anchor to remain within `0.001 m` of the reviewed record.

Drive torque is routed by `LeftDrivenWheelIndex` / `RightDrivenWheelIndex`. The current authored config selects FL/FR (`0/1`), while focused coverage verifies that the one-pair mapping can be changed to RL/RR (`2/3`). AWD/multiple driven pairs are outside M06. The current FWD selection is a project topology decision for the target vehicle. Dynamic torque, tire and surface response values remain provisional because the reviewed donor dynamic fixture is `Missing`.

## Mass and radius interpretation

- Donor root `Rigidbody.mass = 389 kg` is an exact serialized component value. It is not proven to be assembled or curb mass and is not used as the M06 proxy mass.
- M06 uses `650 kg` as `ProvisionalProjectTuning` for the proxy Rigidbody.
- Candidate `tire_stock` mesh radius `0.272667 m` is `NeedsReview`/plausibility evidence only; fitted-wheel identity is unproven. M06 uses that number as a provisional plausibility target, not as an exact fitted donor radius.

## Surface metadata

The simulation type is explicit and independent of renderer materials or terrain texture indices:

| Type | Friction multiplier | Rolling-resistance multiplier | Provenance |
|---|---:|---:|---|
| `Unknown` | `0.70` | `1.35` | provisional fallback |
| `Paved` | `1.00` | `1.00` | provisional design target |
| `Gravel` | `0.72` | `1.45` | provisional design target |
| `Dirt` | `0.62` | `1.75` | provisional design target |
| `Grass` | `0.48` | `2.10` | provisional design target |
| `MudWet` | `0.35` | `2.80` | provisional placeholder |

The M06 builder authors four consecutive 25 m graybox segments for paved, gravel, dirt and grass. `MudWet` exists in config but is not a route segment yet.

This is vehicle-owned prototype metadata. It does not close production-world issue `WORLD-COL-003`: all 164 audited production colliders still use the default layer and no classified PhysicMaterial policy. M06 must not claim that its markers prove production road/terrain parity.

## Bounded route

`M06_BoundedSurfaceTrack` is 100 m long and 12 m wide with simple boundary cubes. The reset pose is `(0, 0.55, 4)` in track-local metres. It exists to exercise the wheel contract and surface transitions without modifying production world content.

The route is explicitly:

- project-authored graybox;
- isolated from the production streaming cells;
- not a donor road transfer;
- not evidence for `road-driving-speed`, full `vertical-slice-route`, garage clearance or streaming-boundary driving.

Those scenarios belong to M06A once a validated production route/fixture exists.

## Replacement requirements

A future backend may replace the prototype if it preserves the `IWheelPhysicsBackend` contract and supplies finite, unit-consistent values. The custom tire backend should add load sensitivity, combined-slip curves, relaxation, surface/wetness response and configurable physics substeps without moving engine/gearbox logic into the backend.

## Current limitations

- Simple ray contacts; no swept tire volume.
- No Ackermann geometry, caster/camber/toe, anti-roll bars or body-roll model.
- One friction-circle clamp with provisional linear stiffness.
- Brake balance is uniform; handbrake is not wired into the command path.
- No validated center of mass, ride height, static compression, ground clearance or fitted tire.
- `VehicleSpeedMetersPerSecond` is Rigidbody velocity magnitude, not signed longitudinal speed.
- The isolated Stopwatch audit cannot measure Unity `Physics.Processing`; physics CPU is unavailable until a later player/profiler capture.
