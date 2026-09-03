# Milestone 11B-R4 — physical Jani/Petteri story traffic

## Outcome

Jani and Petteri are no longer route poses animated as cars. Their loaded
wrappers now contain dynamic Rigidbody chassis, four NWH WheelController contact
units and a project-owned engine/clutch/gearbox/differential. The route supplies
only a look-ahead target; throttle, service brake, handbrake and steering create
the actual movement.

The full loop begins at the locked Perajarvi formation, follows the donor
Village and RoadRace networks into Highway, returns through RoadRace/Village and
passes the Teimo area before looping. Both drivers start in the same direction.
Physical progress survives the schedule-block boundary and is projected from
the actual chassis. The game clock only activates/deactivates the scenario.

## Behavior implemented

- donor range `115–185 km/h` with smooth per-driver variation;
- actual-speed gear selection with launch in first gear;
- changing lane bias rather than centerline pose dragging;
- early obstacle sensing, alternate-lane clearance checks, rolling pass,
  return-to-lane and reverse escape when both lanes are blocked;
- high-speed corner handbrake/drift state bounded by speed, angle and duration;
- PhysX collision incidents at the donor `5 m/s` threshold plus non-teleporting
  recovery;
- NWH wheel steer/spin drives the donor wheel renderers;
- physical RPM, load and selected gear feed the existing spatial traffic audio;
- donor 16:00–02:00 Wednesday/Friday/Saturday/Sunday activation gate;
- 18–32 second deterministic social stop on return to the Perajarvi/Teimo end
  of the loop. This stop is a project-authored Phase 1 approximation until its
  exact donor trigger has been captured.

## Important corrections found by testing

The initial obstacle sphere overlapped the supporting road and held all service
brakes on. Its origin now clears the road by its own radius. Gear selection used
the requested cruise speed and could choose second gear while stationary; it
now shifts from actual wheel speed. Passing originally tracked only the desired
lane offset and could deadlock while fully braked; it now checks actual chassis
lateral displacement, begins the manoeuvre early enough and keeps a controlled
rolling speed while steering around the obstacle.

Starting a fresh session after the 16:00 activation time exposed a second
authority error: `SecondsSinceStart` was being interpreted as route progress,
so a 16:04 start materialized both cars four simulated minutes down the Highway.
Physical story drivers are now registered before the first schedule evaluation.
The schedule only activates the encounter, a new unsaved encounter begins at
progress zero in the locked Perajarvi formation, loaded progress comes from the
Rigidbody chassis, and unloaded progress advances only by the elapsed delta
since dematerialization. Existing saved route progress is still restored.

The final full-Bootstrap reproduction exposed two integration faults that the
isolated vehicle fixture could not contain. First, ordinary game-time
reconciliation could write the logical route pose back into an already physical
wrapper and compete with its live look-ahead target. Logical placement is now
used only for materialization, route changes and save restore. Second, dialogue
setup added a humanoid `CapsuleCollider` to each car. That capsule supported the
chassis on the road while the driven rear wheels spun without moving it. Physical
story cars now keep their authored chassis collider as the interaction surface
and explicitly receive no humanoid capsule.

Traffic audio had a separate double-registration fault. The presenter first
registered its emitter through `AudioEmitterAuthoring`, then the vehicle audio
backend attempted to register the same stable emitter and remained
uninitialized. The vehicle backend is now the sole registration owner. Until
the reserved Wwise events and bank media are authored, the private Phase 1
engine/music/skid/crash clips are routed explicitly through the spatial Unity
fallback instead of disappearing into a ready but unmapped Wwise backend.

## Compatibility

Stable character, vehicle, presentation, route, schedule and save IDs are
unchanged. Existing 00–08A APIs and the accepted UI/weather/world boundaries are
unchanged. Old snapshots remain valid and restore route progress; speed,
maneuver and audio-loop state survive wrapper streaming during the active
session.

## Manual acceptance

1. Start Wednesday/Friday/Saturday/Sunday shortly before 16:00, or begin a fresh
   unsaved encounter at 16:04, in Perajarvi.
2. Confirm both cars start together and travel in the same direction.
3. Follow through town, Teimo, RoadRace and Highway; check that they steer,
   accelerate, shift and react through tire forces rather than sliding on a
   spline.
4. Place a movable cube on a sufficiently wide road. One car should slow and
   pass if a lane is clear; with both sides blocked it should brake and reverse,
   not despawn or appear behind the cube.
5. Check high-speed bends for occasional bounded drift and verify wheels remain
   grounded on elevation changes.
6. Stream the cars out/in and confirm no starter replay, no route jump and no
   full-map engine/music audibility.

Known limitation: the exact subjective aggression and every donor route-choice
branch still require the user's full-loop comparison. Durable crash damage and
story consequences remain owned by later parity rows, not this physics pass.

## Executed validation

- NWH adapter EditMode: `4/4`;
- generated physical story cars PlayMode: `5/5`, including exact two-car
  Perajarvi-formation launch without mutual deadlock, grounded high-speed
  acceleration and a 3 m road-block pass with `0.310 m` maximum observed
  Rigidbody step;
- NPC foundation EditMode: `19/19`, including a fresh 16:04 activation at route
  progress zero and exact formation materialization;
- NPC presentation/streaming PlayMode: `8/8`;
- full production Bootstrap launch: `1/1`; both cars moved from the exact 16:04
  Perajarvi formation (`4.19 m` Jani, `3.03 m` Petteri), remained grounded and
  initialized both persistent vehicle-audio owners;
- Unity spatial audio fallback PlayMode: `8/8`, including RPM-driven local
  story-traffic engine playback;
- shared vehicle-simulation EditMode: `29/29`.

## 2026-08-08 route recovery, Teimo turn and bus-hill correction

The physical chassis remains authoritative when story traffic leaves the route.
Route projection now preserves the retained branch and heading instead of
jumping to a nearby opposite or repeated branch. A bounded rejoin mode removes
passing, drift and wander bias, targets the base lane through tire forces and is
cleared only after the chassis has physically re-entered the route corridor.
Reverse recovery uses a separate escape target behind and to the side of the
vehicle; it no longer reverses while steering toward the same forward target.

The donor Teimo loop geometry is unchanged. Inside its authored handbrake zone,
physical pursuit is shortened to `4..5 m`; the surrounding tight corridor uses
`4.5..6 m`, and the maneuver is capped at `60 km/h`. This makes Jani and Petteri
reach the donor apex at story waypoints `78/79` (`Village 14/15`) before the
slide instead of cutting across the forecourt early.

The bus now has its own no-wander road profile and a narrow physical route
corridor through the wastewater bend. On the following B52..B80 climb, the NWH
backend receives a bounded bus-only hill assist: sequential synchronous-RPM
kick-down on B61..B68, a `0.62` throttle floor when speed is deficient and a
`0.50` launch-clutch cap. The assist changes drivetrain inputs only; it applies
no force, transform drag or teleport and is removed after B80.

Story-traffic materialization also waits for the streamed cell and all manifest
global scenes, preventing Pena or another always-physical actor from spawning
before the global road collider. Corrupt under-map saved poses are rejected and
a catastrophic-fall guard returns an already affected chassis to its retained
safe route pose. Pena's overlapping dirt-road branches use a full-route aligned
projection only when it is nearly stopped with its target behind it.

Executed validation after merging these corrections:

- NPC foundation EditMode: `30/30`;
- traffic road-network EditMode: `16/16`;
- NWH bus gearbox EditMode: `23/23`;
- generated Jani physical off-route rejoin PlayMode: `1/1`, from `10 m` lateral
  displacement to `3.98 m`, with `25.67 m` forward progress, `13.94 m/s` final
  speed, continuous ground contact and `0.279 m` maximum Rigidbody step;
- generated Jani road-block pass PlayMode: `1/1`, returning past the obstacle
  at `13.27 m/s` with `0.265 m` maximum Rigidbody step;
- Teimo authored handbrake-zone PlayMode: `1/1`;
- bus wastewater bend and hill PlayMode: `1/1`, including B44..B82 traversal
  and a zero-speed B66 hill restart without reverse loop or teleport;
- authoritative traffic-audio pose PlayMode: `1/1`;
- terminal in-car articulated ragdoll PlayMode: `1/1`.

Manual full-loop acceptance remains required. In particular, the later bus
climb around B738..B760 is driven by the normal bus gearbox but is not yet
covered by the dedicated hill regression above.

## 2026-08-09 Perajarvi junction, inspection and dirt-road correction

The donor Jani/Petteri waypoint order remains unchanged. Runtime control now
uses progress-directed maneuver corridors instead of spatial trigger spheres at
the overlapping Perajarvi branches. The inspection H2/H3 turns receive separate
early braking and short physical pursuit targets. The Perajarvi outbound bend
brakes from story waypoint 369, holds a 44 km/h core through 381..390 and
releases through 398. The inbound Highway return brakes from 939, holds a
43 km/h core through 953..963 and releases through 975. This prevents the
outbound shoulder/sign cut and the inbound overshoot while preserving the exact
donor centerline and branch direction.

The handbrake request now changes NWH tire behavior, not only maneuver state and
audio. While the bounded handbrake command is active, rear lateral grip releases
to 46 percent and then restores progressively. Steering, wheel torque, collision
and chassis motion remain physical; no scripted yaw or transform drag is used.

Ordinary Pena/FITTAN traffic now ignores only the exact collider confirmed as
supporting its chassis. Real props and vehicles remain obstacles. Dirt-route
projection excludes the donor's long invalid 3718-to-0 closure, keeps the
authored 16..3718 loop, uses heading-aligned recovery at overlapping branches
and applies curve/grade speed control. Non-convex support meshes also use a
bounds-safe reverse escape query instead of the unsupported PhysX
`Collider.ClosestPoint` call.

Executed validation:

- Perajarvi directed speed/preview corridor EditMode: `1/1`;
- generated overlapping Perajarvi branch identity EditMode: `1/1`;
- traffic route geometry and recovery EditMode: `18/18`;
- confirmed support mesh versus real prop PlayMode: `1/1`;
- generated Jani physical handbrake drift PlayMode: `1/1`;
- generated Jani 10 m physical off-route rejoin PlayMode: `1/1`;
- full Bootstrap traffic/dirt-road integration PlayMode: `1/1` in
  `153.19 s`, including Pena travel, corridor, ground contact, no rollover,
  no drivetrain stall and no teleport assertions.

Manual acceptance must still compare the subjective drift angle and exact
entry/exit line at full game speed. The automated pass proves the directed
branch, braking envelope and physical tire response, not final handling polish.

## 2026-08-10 directed story-route, terminal bus and town-excursion pass

Jani and Petteri now follow progress-directed H2 inspection and TrackField
approach/oval/closure/terminal corridors instead of resolving overlapping
branches by nearest position. The Teimo visit contains one saved, one-shot
physical dwell per driver (`6 s` for Jani at story point `96`, `8 s` for
Petteri at story point `94`). Passing is bounded to `6` real seconds or `55 m`,
the authored handbrake request is a `0.88 s` physical rear-grip release, and the
route ends in the donor FULLSTOP formation (Jani `TF201/story2087`, Petteri
`TF199/story2085`). Consumed dwell, remaining dwell, passing and terminal-stop
state survive save/load; no transform drag or route teleport owns the motion.

The bus terminal-stall sequence is now materialized by the generated wrapper.
After the donor `>5 m/s` arm and a confirmed `<=2 m/s` stall for `25` real
seconds (after bounded recovery is exhausted), the drivetrain and audio stop,
brakes hold, lights switch and the service door opens over `1 s`. Four real
seconds later the seated Latanen presentation is replaced by the bus-owned
walker, which moves at approximately `1.2 m/s` and can emit the bounded curse
near the player. Confirmation, shutdown delay, abandoned state, bus pose and
walker pose are persisted. The state-only Latanen definition has no independent
materializing schedule, so it cannot create a duplicate driver.

The ordinary red Fittan town excursion now joins the nearest authored branch
rather than branch zero. TownLoop and GasPump keep separate return progress,
reset rejoin state when switching branches and use town-specific look-ahead,
hysteresis and curve caps. Measured connection gaps are `0.262 m` and `0.419 m`
on entry and `1.419 m` and `1.603 m` on return.

The deterministic import chain also removed three startup blockers: the
state-only vehicle-linked schedule was removed, the Latanen walking clips now
bind from donor skeleton root `37455`, and service-door position/rotation data
is required and converted only for the bus transport. The complete importer
chain exited `0` and regenerated the catalogs/wrappers.

Executed validation:

- NPC Foundation EditMode: `33/33`;
- Traffic EditMode: `48/48`;
- NPC presentation/streaming PlayMode: `12/12`;
- NWH story-traffic PlayMode: `3/3`;
- long Traffic PlayMode: `1/1` in `158.56 s`;
- terminal in-car ragdoll PlayMode: `1/1`;
- authoritative traffic-audio pose PlayMode: `1/1`.

Manual full-route handling, subjective drift shape, bus abandonment staging in
the populated Bootstrap scene and final Wwise mix remain acceptance items. No
row is promoted to `Verified` by this automated pass alone.

## 2026-08-10 conditional user acceptance

The user conditionally accepted the complete NPC/story-traffic pass as the
Phase 1 integration baseline. The current project-owned routes, stable IDs,
save DTOs, physical controllers, presentation bindings and importer output must
remain compatible with that baseline.

This decision is not a blanket `Verified` or `ProductionReady` promotion and
does not open the Phase 2 gate by itself. Subjective handling, drift shape,
route-line refinement, vehicle audio/Wwise mixing, animation and presentation
polish, plus regressions found during the later remaster playthrough, are
explicitly deferred to the Phase 2 backlog.
