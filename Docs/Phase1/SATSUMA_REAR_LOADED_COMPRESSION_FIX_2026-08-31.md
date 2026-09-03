# Satsuma 4A — rear loaded-compression correction

Status: implementation and scoped automated validation passed; bounded manual
Bootstrap check is `USER PASS` as of 2026-09-01. Issue 4A is accepted. Rear
wheel placement is implemented separately in V1d.41 and awaits its own manual
acceptance.

## Scope and frozen authority

The user accepted the V1d.38 airborne travel and non-separating rear shock,
then reported that the rear barely compressed and held the shell too high.
Issue 4A is fixed without changing front suspension, wheel installation,
fasteners, stable IDs, part masses, chassis mass/centre of mass or save DTOs.
General save repair remains explicitly deferred until the entire suspension is
accepted.

Read-only authority:

- frozen build `msc-world-baseline-04a1.1-c3f2f337`;
- `GAME.unity` SHA256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- donor `Wheel.cs` SHA256
  `85FFBF994222E04AC61A32BEBC9CDEAE9C7EFE53DF7D7BF75910EA80BC051945`.

No donor file, FSM or runtime assembly is modified or imported as executable
authority.

## Original mechanic

The original rear arm is not the force-solving pendulum that the old remake
assumed. `Wheel.cs` raycasts from a wheel carrier, calculates wheel-space
compression and applies the resulting force directly to the chassis at the
wheel position. Rear arm/hub/shock geometry follows the wheel target through
the donor IK/presentation chain.

The frozen formula is:

```text
compression = clamp(travel - springLength, 0, travel)
springForce = K * compression
damping = signed piecewise D(compressionVelocity)
totalForce = max(0, springForce + damping)
```

Airborne/full-droop compression and total force are zero. Spring and shock are
selected independently by `Installed`, not by `Bolted`.

| Rear stage | Carrier Y | Travel | K | Maximum spring force |
|---|---:|---:|---:|---:|
| No spring | -0.150 m | 0.14 m | 2 N/m | 0.28 N |
| Stock | -0.165 m | 0.14 m | 21200 N/m | 2968 N |
| Extra-long | -0.180 m | 0.17 m | 29000 N/m | 4930 N |

Shock absent/present damping is `2/2` or `1000/1000 N s/m`. Slow factors are
1, fast factors are 0.3 after 0.3 m/s. Frozen rear anti-roll force is zero.
The bare drum contact is radius `0.0871 m`, width `0.08 m`.

## Why the old remake was wrong

V1d.38 made the travel envelope correct but kept a coil-seat free-length
spring on a 3 kg physical HingeJoint arm. At the new accepted stock droop stop
that model already produced about 2.65 kN at the coil seat, roughly 1.32 kN of
equivalent support per rear wheel. The original produces zero spring force at
the same full-droop state. The remake therefore perched a lightly loaded rear
near full extension.

Trying to encode the donor curve as an explicit hinge force/motor exposed the
deeper architecture mismatch. A one-fixed-step-late drive on the light arm
oscillated or damaged joint constraints; its settled compression changed with
the physics timestep and did not equal `F/K`. Increasing solver iterations,
changing K, mass or centre of mass would only hide the wrong force owner.

Failed diagnostics remain in ignored `Logs/`, including the bounded-motor
runs `codex-rear-load-v1d39-motor-sign-play.xml`,
`codex-rear-load-v1d39-motor-1000-play.xml` and
`codex-rear-load-v1d39-motor-1000-brake-play.xml`. They are evidence for the
architecture decision, not passing verification.

## Implemented correction

`NwhAssemblyWheelSupportController` now enables donor-style rear Wheel contact
only after the trailing arm and brake drum are installed. Its three exact
spring stages preserve carrier height across a live stage switch; the
independently fitted shock overrides damping after the spring stage is applied.
Rear anti-roll is zero.

`SatsumaRearNwhSuspensionController` is the ownership adapter:

1. Arm only: NWH is disabled; the accepted V38 physical hinge and ground
   response remain active.
2. Arm plus drum: NWH becomes the sole ground/spring/damper force owner.
   Arm, drum and fitted road wheel are kinematic presentation, with their solid
   colliders/joints disabled so there is no second solver.
3. NWH compression is converted through the exact donor `atan2` geometry to
   the accepted none/stock/long arm angle. Spring and shock endpoints follow
   the arm-owned mounts.
4. If the drum/support is removed or the adapter is disabled, the authored
   neutral mount is restored before recreating the physical joint. This avoids
   turning the current NWH angle into a new HingeJoint zero.

The rejected native motor code and tests were removed. Legacy serialized
coil-space fields remain hidden compatibility diagnostics only; they no longer
own rear support.

## Executed verification

| Artifact | Result | Coverage |
|---|---:|---|
| `codex-rear-nwh-v39-travel-edit.xml` | 48/48 | none/stock/long endpoints, clamps, angle/compression round trips and invalid inputs |
| `codex-rear-nwh-v39-force-edit.xml` | 60/60 | frozen K and signed slow/fast damping formula; obsolete motor cases removed |
| `codex-satsuma-v39-generated-edit.xml` | 29/29 | four enabled bindings, exact rear stages/contact sizes, two-corner adapter and retained assembly metadata |
| `codex-rear-nwh-v39-lifecycle-play.xml` | 2/2 | arm-only physical; arm+drum NWH-only; removal restores neutral physical hinge; none/stock/long stage swaps |
| `codex-rear-nwh-v39-loaded-play-r4.xml` | 3/3 | stock and long known compression/force plus free-Y chassis settling under symmetric load |
| `codex-rear-nwh-v39-droop-play-r3.xml` | 4/4 | airborne zero compression, exact RL/RR minimum angles, live none/stock/long swaps and shock overlap |
| `codex-rear-nwh-v39-installed-play-r2.xml` | 17/18 | every 4A and non-wheel installed-part case passed; the sole failure is the deliberately deferred issue-5 road-wheel axle contract |
| `codex-rear-nwh-v39-front-regression-play-r1.xml` | 13/13 | front fasteners, steering geometry and spawn regression remained intact |
| `codex-rear-nwh-v39-bootstrap-play-r1.xml` | 1/1 | production Satsuma Bootstrap composition |
| `codex-satsuma-v1d39-rear-nwh-final-build.log` | exit 0 | `PHASE1_SATSUMA_BUILD_OK version=11A-V1d.39` |

The known-load gates use actual NWH contact geometry:

- stock: `981 / 21200 = 0.0462736 m`;
- extra-long: `1471.5 / 29000 = 0.0507414 m`.

Both corners reproduce their target compression within 4 mm and their spring
force within 3 N. A free vertical chassis under two 981 N downward loads settles
on the rear contacts instead of remaining at full extension.

The old broad smoke check expected a bare-drum fixture to lift the chassis by
more than 20 mm. That threshold had no donor basis and became impossible after
the exact `0.0871 m` drum radius replaced the old `0.12 m` proxy. The migrated
test now removes competing chassis-ground contact and verifies the meaningful
contract: grounded rear rays, `compression = mg/(2K)`, spring support close to
the chassis weight and a settled vertical velocity.

All automated 4A gates required for handoff are complete. The obsolete dynamic
road-wheel axle expectation was replaced during the separate V1d.40/V1d.41
wheel-placement step. The broad installed-part suite now passes 18/18 without
changing 4A force ownership. On 2026-09-01 the user confirmed that rear
loading/compression is normal and the wheel installs. This is the bounded
manual acceptance for 4A; wheel seating has a separate manual gate.

## Manual acceptance result

`USER PASS`, 2026-09-01: rear compression/ride response is normal in the live
Bootstrap check. The user also confirmed that the rear wheel now installs.
Its positional offset was not a 4A force/travel failure. The isolated V1d.41
donor-standard correction is recorded in
`Docs/Phase1/SATSUMA_ROAD_WHEEL_SEATING_PARITY_2026-09-01.md` and awaits manual
acceptance.

Retained regression recipe:

In a fresh Bootstrap state:

1. Install both trailing arms and drums; confirm an arm without a drum still
   hangs/reacts physically rather than freezing.
2. Install both stock springs and shocks, then lower the rear onto drums or
   wheels. The rear must visibly take load and compress; it must not remain
   perched at full droop.
3. Raise the rear again. Both arms must return to the accepted V38 droop and
   both shock halves must remain overlapped.
4. Repeat with extra-long springs if practical. Do not use wheel seating as a
   workaround for a suspension failure.

## Small continuation specification

1. Manually validate the separate V1d.41 standard wheel seat at all four
   corners; do not retune the accepted rear suspension to compensate for wheel
   presentation.
2. Add a regular/offset per-part selector only when a donor `wheel_offset`
   family is actually imported.
3. Leave save repair untouched until the entire suspension and wheels are
   manually accepted; no DTO/ID migration belongs in this step.
