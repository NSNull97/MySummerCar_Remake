# Milestone 11B-R3 — exact Amis routes and durable Suski crash rescue

Date: 2026-08-05

## Outcome

This pass supersedes the provisional Village/RoadRace/Highway composition in
the earlier R2/R4 reports. Jani and Petteri retain their physical Rigidbody/NWH
driving, but their project-owned route catalog now follows the exact locked
donor waypoint selection and their terminal collision consequences no longer
reset when a wrapper streams or a native save is loaded.

This is still Phase 1 work. It does not claim production vehicle damage,
complete Suski dating/ending logic or ambient traffic parity.

## Read-only donor evidence

- Locked `GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Jani root/transform `43020`; Petteri transform `48255`.
- Navigation starts with `WaypointStart=228`, `WaypointEnd=294`,
  `DriveTrackfield=true` and eight requested track-field circuits.
- Audited drive sequence:
  `TrackField 228..290 -> Village 0..294 -> RoadRace 0..623 ->
  TrackField 0..201 -> seven additional TrackField 73..201 circuits`.
- Saturday dance-hall branch uses `Dancehall 0..268` and the reverse return.
- `CollisionEvent -> CRASH` uses `DeathSpeedMPS=5`.
- Jani's player challenge requires the player current vehicle/Satsuma near the
  car. The temporary remake trigger must be replaced when the Satsuma driving
  domain is available.
- The rescue destination is the parents' bed visual at project position
  `(168.3286, 1.62, -1027.5336)`, now represented by the stable project anchor
  `anchor.story.suski-rescue-bed`.

No donor FSM, controller, script or runtime assembly is used.

## Implemented

- Separate 2,088-point Jani and Petteri race routes preserve their exact donor
  formation and full ordered waypoint sequence. They are finite `Once` routes:
  after the eighth requested TrackField circuit they stop instead of drawing an
  invented final-to-start chord across the world. Separate 537-point dance-hall
  cycles preserve the out-and-return branch.
- Loaded cars remain Rigidbody-authoritative. Route projection is locally
  continuous and can no longer jump to a distant repeated-lap sample after a
  save or at an overlapping branch.
- The NWH adapter uses sequential RPM-based shifts with a bounded clutch and
  throttle cut instead of independently selecting a gear every frame.
- Obstacle probing uses a three-wide swept fan. Recovery reverses along the
  road reference instead of steering into the last occupied pose.
- A crash at or above 5 m/s publishes a typed incident and leaves the affected
  story car in a fully braked terminal hold. Its physical wreck pose is stable
  across streaming and native save/load.
- Jani's terminal crash hides the in-car passenger and materializes the same
  stable Suski identity as a gravity-enabled 65 kg pickup target. The stages
  are `Passenger`, `AwaitingPickup`, `Transporting`,
  `RestingAtParentsBed` and `Rescued`.
- Dropping or placing Suski within 2.2 m of the stable parents' bed anchor
  locks her to the bed. The next successful player sleep completes the rescue
  and switches the existing `flag.suski.rescued-from-jani-crash` state.
- New optional `traffic.state` schema 1 persists both driver incident records,
  stable identities, crash pose/count/speed/time and the Suski rescue stage and
  pose. It depends on `core.time` and `npc.state`; old v13 saves without this
  optional domain remain loadable without a document-version migration.
- A save made while Suski is actively held resumes her as a loose pickup at the
  captured pose. Carry ownership itself is deliberately not fabricated across
  process startup; all other rescue stages restore verbatim.
- Physical wrappers are now instantiated directly at their retained logical
  route pose and activated only after that placement. A live wrapper separated
  from route authority by more than 80 m is snapped back without projecting its
  world-origin bootstrap pose into saved route progress. The same guard applies
  to ambient road actors and route transports.
- Both Suski states now use BetterMSC presentation. The rescue state builds an
  articulated project-owned ragdoll over the accepted BetterMSC skeleton (11
  Rigidbody bodies and 10 constrained joints); the old donor Suski body, tail
  and cigarette are not valid fallbacks.
- The exact 2,088-point route stores cumulative waypoint progress. Indices
  0..358 use the Perajarvi road profile, 359..982 use the high-speed RoadRace
  profile and 983..2087 use the calmer field/gravel profile. Perajarvi retains
  mild drift at the donor's four authored handbrake zones rather than disabling
  drift for the whole town.

## Temporary trigger and deferred replacement

Until the player Satsuma exists, a new encounter starts at/after 16:04 or after
the player remains within 35 m for two real seconds. This only starts the
encounter; it is never loaded-position authority. Replace it with the audited
nearby-Satsuma rev challenge (approximately 6 m) when the player car and engine
rev event are integrated. This is recorded technical debt, not claimed donor
parity.

## Compatibility

- No accepted 00–08A public API, stable ID or existing save DTO was renamed.
- `npc.state` document v13 remains unchanged; `traffic.state` is additive and
  optional.
- Existing BetterMSC passenger presentation, project audio event IDs, UI and
  vegetation systems were not replaced.
- Generated donor presentation remains `TemporaryDirectImport` and removable
  behind project wrappers/replacement keys.

## Validation

- Unity batch catalog/presentation/bootstrap rebuild: passed.
- NWH focused EditMode: 6/6 passed.
- Traffic EditMode: 5/5 passed after the compatible ambient-state extension.
- Save Integration EditMode: 28/28 passed.
- Full NPC PlayMode: 10/10 passed, including the production-Bootstrap formation
  launch while the additive T1 traffic runtime is installed.
- Corrective NPC EditMode run: 21/21 passed, covering exact profile boundaries,
  four Perajarvi handbrake zones, origin-pose repair, BetterMSC-only Suski and
  articulated ragdoll/save restoration.
- Corrective Traffic EditMode run: 6/6 passed; corrective full NPC PlayMode run:
  10/10 passed in 88.6 seconds.
- Manual combined road/story review remains pending.

## Manual combined test

1. Start on an active day shortly before/after 16:04 and observe the exact
   Perajarvi formation launch, city entry, RoadRace traversal and repeated
   track-field laps.
2. Confirm both chassis accelerate, shift through more than one gear, steer by
   tire forces, keep their wheels grounded and retain local engine/music range.
3. Test a partial obstruction: cars should brake, pass when the adjacent lane
   is clear, and reverse only when boxed.
4. Cause a crash at road speed. The wreck must remain stopped through a nearby
   cell unload/reload and native save/load.
5. For Jani, confirm the in-car Suski disappears exactly once, carry the
   roadside Suski to the parents' bedroom, place/drop her on the parents' bed,
   sleep and confirm her rescued standing state after the transition.
6. Save during each rescue stage and verify it resumes without duplicating
   either Suski or a car.
7. In Perajarvi, verify mostly controlled town driving with short mild drifts at
   the four authored corners; verify the stronger random high-speed behavior
   begins on RoadRace and calms again on the later field/gravel circuits.
8. Approach, leave, stream and save/load both story and ambient traffic. No
   physical wrapper may appear at world origin and route progress must not reset.

## Known limitations

- The encounter trigger is the explicit temporary substitute described above.
- The rescue ragdoll is functional Phase 1 presentation over the temporary
  BetterMSC skeleton. Final production geometry, skinning, joint calibration
  and authored unconscious animation remain Phase 2 work.
- The post-rescue note, date, relationship and ending branches remain owned by
  the later full story milestone.
- Ambient traffic is implemented by the following 11B-T1 pass. Bus, train and
  boats remain absent at this report boundary.

## Exactly one next milestone

Milestone 11B-T1: implement the general road graph, lane topology, ambient
traffic spawning/density, physical route AI and persistent moving-actor domain
on the now-proven vehicle/traffic boundaries.
