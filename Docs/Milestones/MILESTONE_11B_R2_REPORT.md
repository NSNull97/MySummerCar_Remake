# Milestone 11B-R2B — Jani/Petteri road incidents and persistent audio

Date: 2026-08-04

## Scope

R2B completes the bounded Jani/Petteri road-behaviour slice requested after
R2A. It restores the exact Perajarvi formation departure before the locked
Highway route, retains the accepted BetterMSC Suski passenger presentation and
the R2A cruise/pass/return/reverse states, then adds physical collision
incidents, high-speed drift presentation, deterministic post-crash recovery and
per-car audio ownership that survives cell streaming.

This is not a claim that the complete Jani/Petteri story, destructible vehicle
physics or all donor traffic is finished.

## Inspected donor evidence

- Locked `GAME.unity`, SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Formation transform `61904`, Jani/KYLAJANI transform `43020` and
  Petteri/AMIS2 transform `48255` place the cars at
  `(-1173.1444, 3.3897, 123.3123)` and
  `(-1176.1509, 3.4643, 128.1752)` after the audited project translation.
- Their donor heading matches decreasing Village indices. The measured route
  joins are Village `22` to RoadRace `622` (0.339 m) and RoadRace `248` to
  Highway `464` (0.035 m).
- Jani throttle FSM `CollisionEvent -> CRASH`, `DeathSpeedMPS=5`, separate
  `HANDBRAKE`, and `SpeedMin=115` / `SpeedMax=185` values.
- NavigationAI pass, return, brake and reverse state/configuration evidence.
- Jani `EngineSound` uses `idle_ulko_pako3.wav`; Petteri uses
  `idle_sisa4b.wav`.
- Jani `CarBassNPC` selects from the donor `Amistekno` list. R2B locks
  `polaventris-kesa_utare.xm` as the temporary current-cycle track.
- Donor source radii are very large (`EngineSound` max 700 m and Jani music
  max 400 m). They are behavioral evidence, not copied as a remake defect.

No donor FSM, controller, runtime assembly, audio manager or Wwise object is
used at runtime.

## Implemented

- Both cars now materialize in their exact Perajarvi formation at 16:00. A
  once-only distance-weighted Village -> RoadRace departure follows the
  measured joins and hands off at Highway `464`; there is no mid-highway spawn
  or cross-country interpolation. Petteri retains the donor rear offset and
  the same travel direction.
- Obstacle probes remain planning-only and no longer emit fake collision
  incidents.
- Actual PhysX contact, or the future traffic-physics boundary, reports a typed
  incident. The donor `5 m/s` threshold enters `Crashed`; lower-speed contact
  remains a non-crash incident.
- A crash holds the car, then drives it back toward its last safe road pose at
  bounded speed before returning to the route. No catch-up teleport is used.
- High-speed corners can enter a bounded handbrake/drift presentation state;
  slip is clamped and leaves the state after a limited interval.
- Transient motion state (speed, cruise selection, lane/maneuver, drift,
  recovery count and safe pose) is retained by `NpcWorldRuntime` while the
  streamed wrapper is absent.
- Each story car has a project-owned persistent audio owner outside the
  streamed wrapper. The owner follows either the physical or logical pose, so
  engine/music handles do not restart on rematerialization.
- Jani and Petteri use separate temporary engine loops; only Jani owns the
  current music session. Skid and crash have dedicated stable event IDs.
- Unity fallback engine pitch follows emitter-scoped RPM. Jani and Petteri can
  therefore coexist without overwriting each other's fallback pitch state.
- All traffic audio is true 3D linear-falloff audio: engine 65 m, Jani music
  55 m, skid 45 m and crash 80 m. The reported map-wide audibility is removed.
- The event/RTPC/emitter contract is backend-independent and ready for Wwise
  authoring. The private donor-derived clips are currently mapped only in the
  Unity fallback supplemental library; a future Wwise bank must use the same
  stable IDs and attenuation limits rather than introducing gameplay coupling.

## Generated private presentation

`Phase1StoryTrafficAudioImporter` verifies the locked scene and five source
hashes, then creates an ignored `TemporaryDirectImport` library under
`Assets/Game/LegacyImport/RuntimeBaseline/Audio/StoryTrafficR2B`. The committed
manifest contains only hashes, mappings, ranges and deterministic generated
GUIDs. Donor payload remains outside Git and is replaceable by
`presentation.audio.story-traffic.phase2`.

## Validation executed

- R2B audio importer and library validation: passed, 5 clips / 7 events.
- NPC catalog/bootstrap rebuild: passed; the Unity fallback now references the
  NPC voice and story-traffic supplemental libraries.
- NPC EditMode: 17/17 passed
  (`TestResults/Codex_11B_R2B_perajarvi_editmode.xml`).
- NPC PlayMode: 8/8 passed
  (`TestResults/Codex_11B_R2B_perajarvi_playmode.xml`).
- Unity Audio fallback PlayMode: 8/8 passed
  (`TestResults/Codex_11B_R2B_audio_playmode.xml`).
- Tests cover pass/no-teleport, planning-versus-contact separation, the exact
  5 m/s threshold, deterministic recovery, bounded drift, stream-stable
  engine/music ownership, 3D 65 m engine configuration and RPM pitch response.

## Save and streaming boundary

The authoritative driver schedule, route and progress remain in `npc.state`
save document v13. Crash/drift/recovery are short-lived presentation/runtime
incidents and are intentionally not serialized as durable donor state. They do
survive ordinary cell streaming during the active session. Stable IDs, save
schema and replacement keys are unchanged.

## Compatibility

- No 00–08A public API, save DTO or stable entity ID was renamed.
- The accepted BetterMSC Suski mesh, seat pose, neck/arms and rescue flag were
  not rebuilt or changed.
- R2A same-direction route, grounding, wheel motion and physical-cell residency
  remain intact.
- UI and vegetation work owned by other sessions was not modified.

## Manual combined acceptance

At 16:00–02:00:

1. Start at 16:00 and confirm both cars leave Perajarvi in formation through
   Village/RoadRace, then enter Highway without a positional jump.
2. Watch both cars accelerate, corner and pass a cube without teleporting or
   disappearing.
3. Use a full-width obstruction to confirm bounded braking/reverse fallback.
4. Make contact at road speed and confirm a pause plus smooth recovery rather
   than immediate deletion.
5. Cross a streaming boundary and confirm the current engine/music does not
   replay its starter or restart the track.
6. Move more than roughly 65 m away: engine must fade out; Jani music must fade
   by roughly 55 m and Petteri must not emit Jani music.
7. Recheck that accepted Suski remains correctly seated before rescue.

## Known limitations

- Motion remains a project-owned kinematic road driver, not player-car tire
  forces or deformable crash physics.
- R2B recovery is intentionally deterministic; vehicle damage, rivalry/event
  outcomes and the Suski crash/rescue chain are not yet driven by the incident.
- Wwise contains the common vehicle RTPC contract, but the seven private R2B
  events still require explicit Wwise authoring and bank generation before the
  Unity fallback can be removed.
- Bus, train, boats and the remaining traffic population are later 11B work.

## Exactly one next milestone

Milestone 11B-R3: connect Jani/Petteri crash and rivalry incidents to the
project-owned Suski rescue/story-event chain, durable consequences and the
dedicated traffic save domain.
