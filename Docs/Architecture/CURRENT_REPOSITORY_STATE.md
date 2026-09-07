# Current Repository State — Phase 1 audit snapshot

Captured: 2026-09-02

Current editor migration addendum: 2026-09-06. This checkout is now pinned to
Unity **6000.6.0f1** (user-approved update-stream exception) and HDRP **17.6.0**.
Final isolated gates: **1,382 EditMode passed / 0 failed / 1 opt-in skipped**;
**168 gameplay PlayMode passed / 0 failed / 2 opt-in skipped**; **3 graphics UI
PlayMode passed / 0 failed / 0 skipped**. Real Wwise-preferred plus explicit Unity
playback, native Bootstrap reload, active purchased consumables, streaming,
weather, vehicle physics and menu render pixels were exercised. This is not a
standalone build, full-game playthrough, performance result or Phase 1 approval.
The historical 6000.3 editor statements below describe earlier snapshots.

The previous editor was uninstalled after those gates; the external pre-upgrade
project snapshot and user slot remain intact. See
`Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md` for the precise compatibility
boundary, failed diagnostic history, optional old-snapshot test limitations,
unchanged Enviro provenance drift and manual visual-review step. No native save
schema/stable ID migration or accepted 00–08A redesign was introduced.

Current bounded save addendum: 2026-09-05 (Cockpit C2; EditMode `518/518 PASS`,
related fixture PlayMode `63/63 PASS`, production native save `1/1 PASS` in
`27.5199175 s`; manual acceptance pending). For the tested tree this addendum
supersedes the 2026-09-02 missing-helper compile note retained below as
historical snapshot evidence.

Open workspace: `E:\GAYmDev_Studio\MySummerCar_Remake`

## Current status

- Phase 1 Legacy Feature Complete gate: **NOT MET**.
- Locked donor: Steam build `20171487`, revision
  `msc-world-baseline-04a1.1-c3f2f337`.
- Required parity rows: `517`; formally `Verified`: `4`.
- Current formal distribution after documentation synchronization:
  `4 Verified`, `1 KnownDifferenceApproved`, `6 ImplementedUnverified`,
  `202 PartiallyImplemented`, `303 EvidenceCaptured`, `1 Specified`.
- Last green Satsuma generator evidence: `11A-V1d.48`, 125 loose parts, 117
  mounts and 260 fasteners; corrected generated-content rerun `29/29 PASS`.
  Later current-tree edits now fail compilation on missing
  `BuildRustHdrpTextures` and `CreatePaintSurfaceBinding` helpers, so this is
  not a current-tree green build.
- Native save: document v17, atomic storage/recovery/migrations, 15 registered
  production participants. The new required
  `vehicle.satsuma.key-access` schema-1 GlobalState domain persists logical
  `vehicle.satsuma.key` access, including `false`; full-game persistence
  remains incomplete.
- Jobs, Authority, Rally, Media, Progression and Communications have no
  dedicated runtime module root and are not implemented as complete gameplay
  domains.
- Latest current-tree performance evidence is an Editor 1920x1080 home view:
  p95 `16.217 ms`; standalone GPU/Render Thread and 2560x1440 acceptance remain
  missing.
- No current Windows x64 build represents the active working tree. The newest
  existing player is the 2026-07-20 Milestone 08A UI-correction build.
- Current Editor compilation gate: **GREEN** in Unity `6000.3.11f1` batch mode;
  the prior missing Satsuma helper errors are no longer present in the active
  working tree.
- Build Settings currently contain 233 enabled scenes, including five
  prototype scenes.
- Phase 2 remains forbidden.

Authoritative current audit:
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md`.
Row-level authority remains
`Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`; save coverage remains
`Docs/Save/FULL_GAME_SAVE_COVERAGE.csv`.

## Current compatibility boundary

Accepted implementations through 08A remain protected integration baselines.
Later save, item, needs, home, NPC, traffic, economy, service, Satsuma and
vegetation work extends those foundations; it does not make the corresponding
full donor domain `Verified`. Public APIs, stable IDs, scenes, prefabs and save
DTOs must not be replaced without the compatibility process in `AGENTS.md`.

Cockpit C2 keeps key ownership outside the streamed vehicle hierarchy. One
fresh session state is shared with the required save participant and injected
into a valid Satsuma binding after duplicate-ID rejection but before deferred
vehicle restore. Migration `16 -> 17` grants legacy saves fresh access `true`
from an immutable source; normal v17 restore preserves `false`, applies before
vehicle phase 300 and participates in transactional rollback. The delivered
v16 player build `024640` cannot read v17 and must not write a slot after that
slot has been upgraded. No live user save was opened or modified during this
work. The production native-save regression used two clean-session
`RequestLoad` cycles and confirmed v17 `false` plus migrated v16 fresh `true`
before reveal; its isolated GUID test slot was removed. Manual cockpit
acceptance remains pending.

The next active closure boundary is **11A-V1 — full Satsuma state**. It still
requires wiring, fluids, thermal behavior, wear/damage, tuning/electrical,
inspection/Fleetari integration and a donor-compared build-drive-save-stream
route.

## Historical project baseline through Milestone 06A

The sections below preserve the detailed 2026-07-16 milestone record. Their
old status and next-milestone statements are historical and are superseded by
the current snapshot above.

| Item | Current state |
|---|---|
| Unity | `6000.3.11f1 (3000ef702840)` |
| Render pipeline | HDRP `17.3.0`, Linear color space |
| Input | Input System `1.19.0`; project-authored M4 player map and dedicated M06 `Vehicle` map |
| Build scenes | Current settings keep Bootstrap `0`, production cells `6/8`, and append `VehicleSimulationPrototype` at enabled/array index `9` |
| Runtime boundary | Independent Unity 6 runtime; no donor executable/assemblies/assets required |
| Tests | M06A focused EditMode `7/7` and focused PlayMode `8/8` PASS; the preceding M06 focused EditMode `18/18`, focused PlayMode `4/4` and full PlayMode `31/31` also remain passing |

## Milestone 4 runtime state

`MSC.Interaction.Runtime` now owns explicit interaction capabilities, target hosts, bounded raycast query, physical pickup targets, carry physics and carried-object snapshot schema v1. `MSC.Player.Runtime` owns CharacterController movement/crouch, camera yaw/pitch, Input System intent routing, interaction orchestration and debug overlay.

The generated prototype content is:

- `Assets/Game/Player/Content/Input/M4_Player.inputactions`;
- `Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab`;
- `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`;
- `Assets/Game/Player/Content/Materials/M4_InteractionDebug.mat`.

The scene contains light and heavy stable-ID Rigidbody targets, contextual interaction, tool activation and an isolated mount handoff receiver. It reuses the project-owned M3 neutral lighting prefab. No M4 production dependency points to `LegacyImport/ReferenceOnly` or `Imported/DonorGenerated`.

## Ownership and boundaries

- Input produces intent; it does not own gameplay state.
- Candidate discovery is a bounded physics query, not a scene-wide/name lookup.
- The held Rigidbody is explicitly excluded from the query so it cannot hide a mount; unrelated colliders remain occluders.
- Target behavior is exposed through explicit concrete capabilities registered by `InteractionTargetHost`.
- Carried bodies are not parented to the camera.
- Carry ownership restores Rigidbody and collision state on explicit release, disable and destroy.
- The stable-ID snapshot is domain data only; file storage, entity resolution and load application remain in the future Save implementation.
- Mount handoff does not implement compatibility, constraints, fasteners or assembly state.
- `PlayerInteractionController` implements `IInteractionService`; `GameServiceBindings.CreatePartial` can register implemented milestone services without fake future implementations or exposing a service locator.

## Preserved repository state

The worktree was dirty before M4. Existing user modifications to `.gitattributes`, `.gitignore`, `Assets/OutdoorsScene.unity`, HDRP assets and ProjectSettings were preserved. M4 intentionally changes only its own runtime/editor/tests/content/docs plus `ProjectSettings/EditorBuildSettings.asset` and relevant asmdef references. Machine-local configuration and generated test/log outputs remain ignored.

M4 used no donor data and made no donor filesystem changes. The existing donor audit contamination warning and exact M2/M3 provenance remain unchanged.

## Known limitations

- No jump, sprint, full-body presentation or IK.
- Carry spring, mass limits and throw impulse are prototype tuning, not donor-calibrated configuration.
- Placement collision uses a conservative bounds overlap approximation.
- Tool activation has no `ToolDefinition`, inventory or compatibility rules.
- Prototype mount accepts any eligible pickup object and becomes occupied; no unmount flow exists.
- Save snapshot capture exists, but storage/load/resolution/migration are not implemented.
- A manual Game View feel/collision review remains necessary.
- The original working tree remains intentionally dirty with pre-existing Unity/settings/prompt-pack changes; no automatic discard, stash or mixed commit was performed.
- Bounded production-world telemetry and a stationary blocking
  `Physics.Simulate` wall-clock window now exist, but a Windows player capture
  with an available physics profiler marker, GPU timing, memory and an accepted
  60 FPS result still does not.

## Milestone 04A world-layout pilot state

`MSC.World.Runtime` now contains serializable, project-owned layout records and pure coordinate helpers for exactly one bounded garage-road pilot. Durable data is stored at `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json`; it records the garage anchor, seven `DirtRoad` route samples, numeric bounds, source hashes/PathIDs, stable IDs, conversion, dependencies and limitations.

The external staging manifest contains metadata only and is bound by SHA-256. The comparison scene is generated under ignored `Assets/Game/LegacyImport/ReferenceOnly/Comparison/`, is absent from Build Settings and can be removed without breaking production content. The M3 garage and road prefabs are used only as an unchanged visual overlay; the project road is not claimed to match the measured `204.768 m` garage-to-nearest-sample relationship.

No Player or Interaction source, prefab, scene, input asset or assembly definition changed during 04A. No purpose-specific production scene, terrain, building, vegetation, water, vehicle, weather, audio or save implementation was added.

## Milestone 04B reference-data state

`MSC.Core.Runtime` now owns the donor-independent reference schema, stable IDs, deterministic serialization, migration, unit conversion and validation. The evidence dataset is `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureDatabase.json` (`04B.4`); separate current remake tuning is `ReferenceTuningOverrides.json` (`04B.5`). It contains no donor binary payload and performs no donor file I/O.

`MSC.Editor` owns the dashboard/import/manual-observation/evidence-resolution workflow and batch validator. Absolute roots are resolved only from ignored local configuration. `Docs/ReferenceCapture/` owns procedures, index, source map, missing queue and session log. Raw screenshots, video and audio stay outside Git.

The current dataset `04B.4` contains 40 measurements plus 11 behavior fixtures. Static world/player/vehicle facts are imported where sources are unambiguous. The representative rear-left drum has a traced candidate trigger, install/removal gates, one `BoltPM` marker, discrete `0..8` endpoints, wrench `14` and scroll mapping, three clean runtime repetitions and a user-confirmed wheel-installed removal blocker. Both assembly requirements are `Covered` and the fixture is `Ready`. Runtime sprint/crouch/interaction, powertrain, vehicle dynamics, time/weather, audio and UI values remain explicitly `Missing`/`Partial`; fitted-wheel identity and curb mass also remain `Partial`. M4 tuning is not reclassified as donor evidence.

## Milestone 05 assembly state

`MSC.Vehicle.Assembly` owns immutable definitions, mutable runtime state, mount/fastener instances, explicit dependency graph, deterministic queries, operation results, validation and schema-v1 DTOs. It depends only on Core and Interaction. Player and Interaction keep their M4 responsibilities and communicate through the existing handoff/tool/context capabilities.

The shared `M4_FirstPersonPlayer.prefab` also owns a presentation-only
`CrossdotPresenter`. Its 2026-09-02 revision keeps the centre dot, substitutes a
filled open palm/check/cross for pickup, valid installation or valid removal,
and renders at most three current actions in the lower-left canonical safe
frame. Rounded keycaps and semantic mouse-button/wheel glyphs reflect live
bindings. A bottom-centred dynamic stack combines explicit raycast target names
with transient subtitles through `IPlayerSubtitleSource`. A hidden Alt-held
layer swaps ordinary rows for the existing H/M/N gestures without showing an
Alt tutorial. The fixed-capacity snapshot, semantic removal marker and optional
directional wheel capabilities extend the accepted interaction boundary without
changing gameplay authority. Player
builder `1.2.1` now reproduces the donor-sized constant traversal capsule and
the separated `LeanPivot`, `CameraPivot`, `ImpactPivot` and `LookPitchPivot`
hierarchy. The lean hinge uses the exact donor local `Y = -0.3 m`, while the
camera has a `1.7 m` body arm and posture remains camera-only. Historical
  focused Player/Interaction historical tests pass `29/29` EditMode and
  `12/12` PlayMode; the expanded context-action filter passes `88/88` EditMode,
  isolated Alt input passes `1/1`, and the wider player-flow class is `8/9`
  with one unrelated carry-spring tolerance failure. Locomotion parity passes
  `6/6`, player-audio integration passes
`2/2`, and regenerated real M4 garage/house traversal passes `1/1`. Active
donor-world pit and irregular-wall feel remain manual.

The 2026-08-10 traversal correction replaces the former `1.8 m x 0.32 m`
controller with the exact donor `0.5 m x 0.12 m` capsule, retains the `0.4 m`
step in every posture and moves crouch/deep-crouch into the view/head-clearance
rig. Forward lean is a BetterMSC-evidenced, sphere-cast-limited `40 degree`
body arc, entering at project-tuned `150 degrees/s` and returning smoothly at
`120 degrees/s`. A project-owned impact event, camera kick and short eyelid
response fire only for a forward speed above `3 m/s` against a near-frontal
wall. A typed audio presenter reuses `audio.event.interaction.impact` through
the active backend and shared player emitter. The mod DLL is not a dependency.

Post-M05 scale correction makes installation preserve a part's world scale even when consuming an older scaled mount pose. Vehicle Assembly builder `1.1.0` additionally separates unit-scale logical mount transforms from scaled debug marker geometry. Focused M05 PlayMode passes `8/8` both against the current scene and a freshly generated scene.

The reproducible scene `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity` contains 15 clean project-authored prototype parts, 14 mount points and the M4 player prefab. The rear-left drum consumes the `04B.4` behavioral fixture without donor runtime dependencies. Static validation, 22 focused EditMode tests and 8 focused PlayMode tests pass; the 10 000-query performance audit records 0 managed allocations and 0 graph mutations.

## Milestone 05A bounded world-remaster state

The production layer now has a full 13,509-record replacement registry, 51 discovered zone/status groups, 263 grouped manual-art tasks and deterministic pilot production cells for `cell_0_-3` and `cell_0_-2`. The home pilot contains project-authored terrain/road/ditch, home and garage shells, a representative interior, six moving hinges, props/infrastructure and 64 LOD spruce instances. It is integrated with the existing M4 player and M05 assembly scene.

Coverage is deliberately narrow and neither production cell has donor-parity
acceptance. Historical registry rows still describe bounded generated
candidates, while the current fidelity decision for both cells is `Rejected` /
`NeedsRework`; neither may be presented as `Approved` or `Verified`.
Production dependencies remain donor-binary independent.

The next recommended milestone is exactly `Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md`, after manual acceptance of the pilot. Final terrain/road measurements, hero modelling, authored textures, map-scale vegetation/water/infrastructure, HLOD and standalone GPU profiling remain explicit work.

The freshly reinstalled donor installation has different `sharedassets3.assets` / `.resource` hashes from the frozen 04A1 extraction provenance. The 04A1 records were intentionally not rewritten; current full EditMode regression therefore has one expected world-transfer provenance mismatch until a separate audited extraction/reconciliation is performed.

## Milestone 05B.1 gate state

Bounded remediation achieved `PilotGate`. Bootstrap owns a production streaming installer for exactly `cell_0_-3` and `cell_0_-2`, retaining build indices `6/8`. Fresh evidence covers two lifecycle cycles, a real M4 `CharacterController` traversal and four bounded 1920x1080 performance locations. It does not establish a production road-driving route, streaming-boundary vehicle behavior, production surface-material policy or an isolated physics CPU counter.

## Milestone 06 simulation state

`MSC.Vehicle.Simulation` owns a separated fixed-step model with central config/provenance, state/DTO, input/backend/prerequisite contracts, telemetry, an explicit single-pair powertrain graph and small subsystem nodes. Config indices map generic left/right driven-wheel nodes to two distinct wheels; the authored config uses FL/FR (`0/1`), so the current prototype is FWD, while AWD remains future work. This FWD selection is a project topology decision for the target vehicle; numeric dynamics remain provisional because the donor dynamic fixture is `Missing`. `MSC.Vehicle.Runtime` owns the fixed-step host, cached assembly adapter, input router, simple raycast/PhysX backend, reset, presentation, development telemetry and a presentation-only vehicle-audio bridge.

The bounded M06 logical assembly fixture has 12 representative parts and is separate from the moving graybox Rigidbody. This makes installed/secured prerequisites explicit without claiming that placeholder logical masses already determine proxy mass or center of mass.

Reviewed geometry is limited to the four exact derived wheel anchors, `2.334 m` wheelbase and `1.2600002 / 1.2060003 m` tracks. Donor root `389 kg` is not curb/assembled mass. Candidate `0.272667 m` radius remains `NeedsReview`/plausibility only. All donor dynamic fixtures are `Missing`; all current dynamics remain `RemakeDesignTarget` / `ProvisionalProjectTuning`.

The first manual drive confirmed start/run, shifting, stall and RPM behavior but exposed startup creep near `5 km/h` and visible view shake. The bounded remediation now initializes suspension history without a synthetic damper impulse, reports gravity-plane speed, prevents passive tire force from overshooting through zero, settles only a level startup/reset pose and uses a detached smoothed chase camera. Final focused PlayMode is `4/4 PASS`: a six-degree slope remains free to roll, and a later external `WakeUp()` plus `0.05 m/s` velocity is not re-slept. The user accepted the post-remediation drive/audio recheck for the bounded basic prototype on 2026-07-16.

The 100 m paved/gravel/dirt/grass route is an isolated graybox test fixture, not world parity. The refreshed isolated performance audit records `2.85809 / 4.09848 / 6.49453 us` per pure tick at `1/2/4` substeps, `7.71657 us` per backend iteration, `0.243964 us` per telemetry iteration and zero measured allocations. Production issue `WORLD-COL-003` and an isolated Unity `Physics.Processing` measurement remain open.

The local Editor prototype can optionally load seven hash-pinned Satsuma diagnostic clips from frozen external staging through the typed `IVehicleAudioBackend` boundary. `VehicleAudioPresenter` samples transitions at `FixedUpdate`; PlayMode asserts `StarterEngaged -> StarterDisengaged -> EngineStarted`. No clip, absolute path or serialized `AudioSource` is stored in the scene or repository; player builds and simulation remain independent. Missing local staging remains a silent fallback rather than a simulation-test failure. This is `TemporaryDirectImport`/`ReferenceOnly` perceptual feedback, not production audio or parity.

## Milestone 06A physics-validation state

`MSC.Vehicle.Simulation` now also owns the validation profile/comparison
contracts, while `MSC.Vehicle.Runtime` owns the scripted validation rig and
per-tick telemetry capture. `MSC.Editor` owns the M06A course/profile builder,
strict validator, dashboard, performance audit and evidence exporter. The
development-only scene is
`Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity`;
it is not a production build scene.

The builder and strict validator pass. Focused EditMode is **7/7 PASS** and
focused PlayMode is **8/8 PASS**. The PlayMode suite covers repeated
start/idle/launch/braking, coast-down, steering, suspension bump, hill start,
typed surfaces, the bounded production-world transition and a real-backend
manual `Physics.Simulate` performance window. Durable outputs include schema-v4
`Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json` and seven CSV telemetry
captures under `Docs/VehicleValidation/Telemetry/`.

The pure managed audit records `2.571855 / 3.55201 / 5.962805 us/tick` at
`1/2/4` substeps, `6.04886 us/tick` with telemetry and `6.95269 us/tick` for
scripted validation, with zero measured allocations. Production telemetry
after a 50-frame warmup records mean root + backend `0.038197 ms`, linear p95
`0.0461 ms` and maximum `0.0629 ms`. The stationary real-backend window records
combined means `0.038737 / 0.036251 ms` and blocking `Physics.Simulate` means
`0.024990 / 0.023025 ms` with telemetry consumer off/on, zero allocations,
four contacts and no invalid states.

The technical production route advances `12.255066 m` to the streaming
boundary at `z=-1024` with four wheel contacts; an isolated next-cell contact
probe at `z=-970` also retains four contacts. This proves the bounded
collision/streaming fixture only. Both production cells remain `Rejected` /
`NeedsRework` for donor visual and spatial parity.

## Historical M06A next boundary — superseded

The M06A automated gate is PASS and the user accepted its bounded prototype
baseline on 2026-07-16; status is `Accepted / HumanAccepted`. The `Physics.Processing`
ProfilerRecorder was unavailable even though blocking `Physics.Simulate`
wall-clock was measured; GPU timing and Windows-player 60 FPS acceptance remain
a manual evidence gap. The next and only next milestone is
`Prompts/06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md`.
