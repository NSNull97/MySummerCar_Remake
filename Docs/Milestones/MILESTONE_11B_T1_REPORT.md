# Milestone 11B-T1 — road graph and ambient traffic

Date: 2026-08-06

## Outcome

The project now has a project-owned traffic module rather than treating Jani
and Petteri as the whole road population. Ten persistent ambient actors use the
exact locked donor `VehiclesHighway` multiplicity and seven replaceable vehicle
archetypes. Logical actors continue offscreen; within the bounded player area
their wrappers become dynamic Rigidbody vehicles using the licensed NWH wheel
contact backend and project-owned control code.

This is a Phase 1 implementation boundary, not final traffic parity. Manual
long-drive density, collision-response and audio acceptance is still required.

## Read-only donor evidence

- Locked `GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Exact direct children of donor `VehiclesHighway`: three Victro, two Lamore,
  and one each of Truck, Polsa, Fittan, Svoboda and Menace.
- Eight exact donor route records are transferred: Highway, DirtRoad, Village,
  HomeRoad, Dancehall, DragRace, RoadRace and TrackField.
- The generated catalog contains 7,715 route points and 20 directional graph
  connections. Known serialized joins are preserved where available; remaining
  joins are bounded nearest-point connections below three metres.
- Menace appearance probability is 10 percent on weekdays and 40 percent on
  weekends. Project-owned deterministic retry attempts avoid repeating the same
  failed roll forever.

No donor traffic controller, PlayMaker FSM, script, old physics component or
runtime assembly is used.

## Implemented

- New `MSC.Traffic.Runtime` module with immutable road/presentation catalogs,
  exact linear route evaluation and persistent actor state.
- Ten stable ambient actor IDs and seven `TemporaryDirectImport` wrapper
  archetypes: Victro, Lamore, Truck, Polsa, Fittan, Svoboda and Menace.
- Physical materialization at 440 m, hysteretic removal at 520 m and a cap of
  six simultaneously physical ambient actors.
- Offscreen actors advance independently of loaded scenes and do not use donor
  hierarchy paths or instance IDs as runtime identity.
- Near-player wrappers have authored chassis collision, four NWH wheel
  controllers, project-owned vehicle simulation configuration and physical
  steering/throttle/brake authority.
- Existing optional `traffic.state` schema 1 gains an additive ambient-actor
  array. Old saves without the array remain valid; route progress, spawn state,
  circuit count and deterministic spawn-attempt count round-trip through native
  save/load.
- Bootstrap owns and validates both traffic catalogs, initializes the runtime,
  and includes it in the existing save session composition.
- Temporary per-actor spatial audio is routed through `IAudioBackend`. It uses
  the Petteri fallback profile only as a bounded development substitute;
  per-archetype Wwise events/media are still required.

## Compatibility

- No accepted 00–08A API, stable ID or save document version was renamed.
- The traffic domain remains optional schema 1; the new array is additive.
- Jani/Petteri remain NPC/story authorities and keep their existing stable IDs.
- All donor presentation is removable behind project-owned wrapper and
  replacement keys.
- Existing UI, weather, vegetation and unrelated audio work was not redesigned.

## Validation

- Unity batch import/catalog/bootstrap build: passed.
- Generated report: 8 routes, 7,715 points, 20 connections, 10 actors and 7
  physical presentation archetypes.
- Traffic EditMode: 5/5 passed.
- Save Integration EditMode: 28/28 passed.
- Full NPC PlayMode: 10/10 passed in 89.44 seconds, including production
  Bootstrap initialization and story-traffic departure.
- Unity compilation after the final DTO/spawn correction: passed.

## Manual comprehensive test

1. Drive a long Highway segment on weekday and weekend saves and compare actor
   count, direction and Menace frequency without restarting the whole game.
2. Cross the 440/520 m residency boundary repeatedly; verify no duplicates,
   teleports, starter one-shots or map-wide audio.
3. Save with visible and offscreen actors, reload, and confirm their stable
   route positions and spawn state continue.
4. Block a lane and collide with representative car/truck bodies. Confirm the
   physical wrapper reacts and does not skip through the obstacle.
5. Observe wheel contact, steering, shifts and speed over terrain/road changes.
6. Confirm Jani/Petteri and Suski R3 behavior is unchanged while ambient traffic
   is active.

## Known limitations

- The T1 population is the audited Highway group. Exact density/activation
  cadence still needs an executed donor comparison.
- Graph connection intent and loop flags beyond serialized known joins need a
  manual original-game traversal comparison before `Verified` status.
- Ambient obstacle planning currently reuses the proven story-traffic physical
  driver boundary; richer lane negotiation and damage consequences remain open.
- Per-model engine, horn, skid and crash media/final Wwise mix are not complete.
- Bus, train, boats and their service/crossing rules are owned by 11B-T2.

## Exactly one next milestone

Milestone 11B-T2: implement the public bus service, train crossings, two AI boat
identities and remaining scripted route actors from hash-locked donor evidence,
including streaming/save behavior and temporary spatial audio boundaries.

## 2026-08-08 corrective appendix

The current traffic bugfix changes residency without changing stable actor IDs
or the optional `traffic.state` schema:

- selected-root ordinary traffic advances logically by real runtime seconds
  offscreen, then materializes at `440 m`, is retained through `520 m`, and uses
  a retained-first cap of six physical wrappers;
- offscreen ordinary collisions are not simulated, and game-clock jumps do not
  advance route distance;
- Pena and the separate Saturday context are outside the ordinary cap;
- Jani, Petteri, the bus and Pena remain physically simulated regardless of
  player distance by explicit user decision, preserving race/crash/story risk;
- the dense Jani/Petteri donor race route is evaluated linearly, Teimo
  handbrake zones use `7..10 m` look-ahead, and braking begins approximately
  `245 m` before RoadRace exit;
- the bus physical driver uses `60..82.5 km/h` and no generic wander/drift.

This appendix records implemented source behavior. Automated validation passed:
`79/79` combined EditMode, `4/4` focused physical story PlayMode and `1/1`
long traffic/bus PlayMode. Manual complete-route/service acceptance is still
pending; no traffic feature is marked `Verified` by this correction.
