# NWH Vehicle Physics 2 v13.5 — story-traffic integration

Status: locally licensed third-party runtime dependency, integrated for the
private Phase 1 build. NWH is not donor content and is not simulation, route,
AI, save or audio authority.

## Source fingerprint

- user-provided package: `NWH Vehicle Physics 2 v13.5.unitypackage`;
- byte length: `467086555`;
- SHA-256: `171FB37CD3BC0D62C7F6C05762AB362B4BF02549FE048C810485AE513354D817`;
- imported vendor root: `Assets/NWH`;
- the embedded package copy of `Packages/manifest.json` was deliberately not
  imported.

Unity 6 renamed the two `PhysicMaterial` combine properties used by the
package. The local vendor compatibility patch in
`Assets/NWH/WheelController/Scripts/WheelController.cs` changes only
`bounceCombineMode`/`frictionCombineMode` to
`bounceCombine`/`frictionCombine`. No NWH behavior was reimplemented inside the
vendor folder.

## Ownership boundary

`NwhWheelPhysicsBackend` is the only wheel-contact adapter used by story
traffic. It accepts project-owned per-wheel drive, service-brake, handbrake and
steering commands. `NwhStoryTrafficVehicleMotionBackend` owns the adapter-facing
powertrain host and exposes physical RPM/load/gear telemetry.

The following remain project-owned:

- `VehicleSimulationRoot`, engine, clutch, gearbox and open differential;
- Jani/Petteri route identity, schedule and persistent progress;
- throttle/brake/steering decisions, lane variation, passing, reversing,
  bounded drift and crash recovery;
- streaming handoff and save DTOs;
- audio event IDs and `IAudioBackend` routing.

During ordinary loaded driving no system writes the chassis pose. Pose snaps are
restricted to initial materialization, restore and genuine streaming
discontinuities. Unloaded traffic uses a coarse route projection at the midpoint
of the donor 115–185 km/h range; once materialized, route progress is projected
from the actual Rigidbody position and cannot advance with the game clock.

## Transferred donor configuration

The locked `GAME.unity` hash is
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The importer transfers the reviewed Jani/Petteri mass, driven axle, gear/final
drive ratios, wheel radius, suspension travel/rates, front/rear service brake,
handbrake, steering and the FSM speed interval. Project-authored interpolation
is used between the donor torque landmarks.

The donor activation gate is 16:00–02:00 on GlobalDay 3/5/6/7, mapped to the
project's Wednesday/Friday/Saturday/Sunday mask. Time activates the scenario; it
does not move a loaded car.

## Automated evidence

- NWH adapter EditMode: `4/4`;
- NWH generated-car PlayMode: `4/4`; both cars accelerate while grounded and
  Jani reaches `25.028 m/s` in third gear without pose dragging;
- physical road-block PlayMode: Jani enters the passing state, clears a 3 m
  cube and advances beyond it; maximum observed Rigidbody step is `0.310 m`;
- NPC maneuver/recovery/audio PlayMode: `8/8`;
- NPC foundation EditMode: `18/18`, covering physical progress authority, full
  route and donor day/time gates;
- shared vehicle-simulation EditMode: `29/29` after splitting front/rear
  service-brake torque.

## Remaining acceptance risk

Automated fixtures prove the physical chain and bounded behavior, not final
feel on every donor road. Manual Play Mode review is still required for the full
Perajarvi/town/highway loop, two-car interactions, high-speed cornering, drift
frequency, terrain grounding, streaming transitions and the Wwise mix.
