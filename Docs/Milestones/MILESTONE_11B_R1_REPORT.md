# Milestone 11B-R1 — story-traffic road-network slice

Date: 2026-08-04

## Scope

This is the first bounded 11B integration slice, not completion of the traffic
milestone. It replaces the provisional Teimo-derived review loops used by Jani
and Petteri with the locked donor highway route and adds a project-owned
near-player vehicle-motion/audio boundary. Accepted Suski passenger
presentation, UI and vegetation were not redesigned.

## Donor evidence inspected

- locked `GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- `TRAFFIC/Routes/Highway`: 1,889 contiguous transforms;
- `TRAFFIC/Routes/BusRoute`: 1,084 transforms;
- `TRAFFIC/Routes/DirtRoad`: 3,719 transforms;
- Village, HomeRoad, Dancehall, Dragrace, RoadRace and Trackfield routes;
- both eight-point lake routes and both train endpoint pairs;
- AMIS setup evidence: the normal road presence window is 16:00 through
  02:00. Rally, crash and story-event gates remain separate later work.

All source access is Editor-only and read-only. The importer rejects a scene
whose hash or exact route topology differs from the locked evidence.

## Implemented

- `Phase1TrafficRouteEvidence` extracts and validates the complete locked route
  set, applying the already audited legacy-world translation.
- Jani and Petteri retain their existing character, vehicle, route and home
  stable IDs. They now start on adjacent locked Highway anchors and travel in
  the same direction around the exact 1,889-point loop, matching their shared
  donor race formation. They are visible from 16:00 through 02:00 and hidden
  during the daytime inactive window.
- Their presentation uses acceleration/braking, bounded steering, Rigidbody
  interpolation, obstacle casts, collision reporting, wheel rotation and
  road/terrain height sampling instead of direct per-tick teleportation.
- The existing `IAudioBackend`/`VehicleAudioEmitterBackend` receives
  project-owned engine, RPM, load, throttle, speed, wheel-speed and braking
  parameters. Stable music event hooks exist for each story car; actual donor
  music media mapping is not claimed by R1.
- Streaming restoration can resynchronize a newly materialized wrapper while
  NPC simulation and the existing `npc.state` domain remain authoritative.
- The importer-owned wheel-contact correction remains inside each generated
  wrapper and is no longer added again at runtime. Both car roots therefore sit
  on the sampled road surface and align to its normal.
- Ordinary route samples are queued while an obstacle blocks the physical car.
  A blocked car no longer interprets its growing simulation lag as a streaming
  discontinuity and teleports beyond the obstruction.
- Traffic presentation no longer posts `VehicleEngineStarted` when a streamed
  wrapper appears. A project-owned four-gear RPM/load approximation drives the
  existing audio parameter boundary; traffic music remains deliberately
  unowned until a persistent music source is implemented.

## Validation executed

- Unity compilation after the route/catalog rebuild: passed.
- NPC EditMode suite: 17/17 passed
  (`TestResults/Codex_11B_traffic_fix_editmode.xml`).
- NPC PlayMode suite: 6/6 passed
  (`TestResults/Codex_11B_traffic_fix_playmode_final.xml`).
- The new PlayMode coverage verifies one-time ground-contact correction,
  obstacle braking, no blocked-route teleport, continuation after removal,
  non-flat RPM/gear response and no starter one-shot on streaming
  disable/enable.
- Full-game save-coverage validator: 4/5 passed. Its sole failing project-wide
  check now reports only pre-existing unrelated HOME domain/contract and two
  presentation-owner records; no 11B-R1 row remains in the failure list
  (`TestResults/Codex_11B_save_coverage2.xml`). Those unrelated records were
  not changed in this traffic slice.

## Compatibility

- No save-document or NPC-domain schema change.
- Accepted Suski wrapper/pose and rescue flag remain unchanged.
- Existing Jani/Petteri stable route IDs and home-anchor aliases are retained,
  so current document-v13 saves restore without migration.
- No accepted 00–08A UI, weather, world-streaming or vehicle API was replaced.

## Manual acceptance required

At approximately 17:00, observe both story cars on the highway and verify:

1. they follow the road rather than Teimo's shop route;
2. turns are continuous and do not cut across roadside objects;
3. body and wheels remain grounded over representative hills;
4. a safe obstruction causes braking and the car continues after it clears;
5. neither car teleports through the obstruction while waiting;
6. engine pitch/load follows acceleration without a starter sound when a cell
   streams in;
7. accepted Suski seat, neck, hands and facing are unchanged.

## Known limitations

- This is a kinematic AI presentation layer, not the player-car tire-force
  simulation and not a claim of physically driveable traffic vehicles.
- Donor high-speed choice (115--185 km/h), lane changing/passing, burnout,
  handbrake/drift, crash and recovery behavior is evidenced but not yet
  implemented by R1. R1 still follows the road route with bounded
  acceleration/braking.
- Rally/drag/crash event graphs, damage and recovery are not yet installed.
- Donor radio/music clips are not yet mapped to the stable music hooks.
- Ambient highway/dirt traffic, bus, train and boats are still 11B work.
- Full `traffic.state` persistence and long-route performance gates remain open.

## Exactly one next milestone

Milestone 11B-R2: donor-faithful Jani/Petteri racing behavior on the locked
Highway route, including speed choice, same-direction formation, lane
selection/passing, drift/handbrake events, collision recovery and persistent
engine/music audio ownership.
