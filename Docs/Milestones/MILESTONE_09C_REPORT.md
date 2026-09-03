# Milestone 09C — progress report

Status: `UserAccepted / Closed` (2026-07-31). The implemented Phase 1 baseline
for locomotion, needs, HUD, home, sauna and the bounded domestic life loop was
manually checked and explicitly accepted by the user. Remaining calibration and
presentation polish listed in this report are tracked as later parity/polish
work and do not keep Milestone 09C open.

## Completed in this batch

- Extended the existing `CharacterController` player instead of replacing the
  accepted Player/Interaction architecture.
- Added run, jump, two crouch levels, obstruction-aware forward lean and a
  project-owned locomotion-state output.
- Preserved legacy binary-crouch save compatibility while adding the authored
  posture enum.
- Added `MSC.Needs.Runtime` as the project-owned needs state owner.
- Bound the existing beer consumption action to provisional `-20` thirst,
  `+8` intoxication and `+4` urine effects. Exact donor values remain
  calibration-pending.
- Added delayed digestion/metabolism buffers: hunger, thirst, body-weight and
  intoxication effects are absorbed over game time rather than applied in the
  same frame as item use. Alcohol also clears progressively.
- Bound game-time advancement, locomotion running modifiers and 09B item-use
  deltas through explicit dependencies.
- Added an idempotent game-time snapshot watchdog to the needs runtime. The
  event path remains authoritative, while the watchdog prevents passive needs
  and queued digestion from silently stalling; paused game time still produces
  no needs progression.
- Beer-case Rigidbody mass now interpolates from 9 kg when full to a
  provisional 1.5 kg empty-container mass as its 24 contained bottles are
  dispensed. The value is also recalculated after save restore.
- Added `player.needs` native-save coverage. Save document version 5 and needs
  schema 3 preserve current physiology, hangover and unabsorbed effects;
  migrations retain compatibility with version 1-4 development saves.
- Reworked the existing 08A HUD geometry toward the accepted reference:
  wider panels, larger type, consistent icon containers, visible percentage
  values, stronger tracks and row separators.
- Refined the HUD typography and need bars: labels use the regular face and the
  bar fill changes width on a sliced rounded sprite, preserving circular caps.
- HUD surfaces use a stable translucent black fill. They do not capture or blur
  the live camera and therefore do not add per-frame blur work.
- Replaced the main-menu performance card's static FPS number with a live
  48-column history graph sourced from the last 240 unscaled frame samples.
  Presentation refreshes at 4 Hz to avoid adding per-frame UI churn.
- Made `slot-01`, `slot-02` and `slot-03` explicit manual-save targets even
  before their folders exist. Empty slots can be selected and written directly,
  so saving no longer depends on first changing to another discovered slot.
- Fixed needs and weather simulation freezing after a save load. Pause-menu
  state is now normalized out of `core.time` both when writing a slot and when
  reading older development slots that captured `isPaused=true`.
- Need-bar fills now use two fixed circular caps and a flat centre segment.
  Low values therefore remain circular/capsule-shaped instead of tapering when
  the fill width approaches the sprite border radius.
- Added a project-owned Phase 1 lighting/probe catalog generated from reviewed
  donor fixture transforms without importing donor lights or behaviours. The
  active catalog contains 42 runtime lights and 6 local reflection probes.
  Streetlights are downward cool-white spots with soft shadows; all content
  follows streamed cell ownership.
- Enabled soft shadows for every generated light. Interior/exterior point-light
  shadows use HDRP on-demand caching while streamed streetlight spots remain
  dynamic.
- Added broad HDRP reflection-probe blending and reduced probe contribution to
  remove visible rectangular transitions around Teimo and other locations.
- Applied one photometric streetlight profile to all 12 fixtures: 353.6777 lux
  at 12 m, 6,570 K, 107/138-degree spot cones, zero indirect contribution and
  dynamic 512 px shadows. The two Teimo lights retain manually verified
  lamp-head positions, rotations and 34 m / 16 m ranges; other fixtures derive
  their lamp-head placement from reviewed donor geometry.
- Constrained the Teimo/store reflection probe and reduced its multiplier to
  0.08 (0.18 elsewhere), preventing the bright shop interior from being
  reflected across the surrounding road and terrain.
- Added the missing hallway and player-bedroom fixtures. Bootstrap now starts
  beside the player's bed facing the bedroom door.
- Replaced opaque `LegacyWorld_<hash>` hierarchy labels with readable
  category/object/location/source-ID labels while preserving every stable ID,
  replacement key and streaming binding.
- Updated the default clock rate from the provisional 20-minute day to the
  measured donor `Clock` FSM rate: `MinutesAdd=0.2`, or 120 real minutes per
  full game day at time scale 1. Enviro sun/moon transforms interpolate between
  bounded presentation updates so shadows no longer jump once per clock tick.
- Applied the user-approved 08A HUD exception: needs remain top-left, time and
  money move to the top-right, and a lightweight FPS panel is placed at the
  bottom-right.
- Added the active `FPS COUNTER / СЧЁТЧИК FPS` gameplay setting. It is enabled
  by default, persists in UI settings schema 3 and migrates schema 1/2 files
  without silently disabling the counter.

## Evidence and calibration status

Standing/crouch/deep-crouch eye heights, gravity, base jump height, step offset
and skin width use the frozen GAME evidence already recorded in the parity
matrix. Run speed, posture timing, lean distance and all passive/running needs
rates remain explicit project tuning values pending repeated donor measurements.
They are not classified as donor-verified.

## Checks executed

- Unity 6000.3.11f1 batch script compilation:
  `Logs/M09C_NeedsHud_Compile.log` — PASS, no C# errors.
- Input Action asset JSON parse — PASS.
- `git diff --check` — PASS.

Additional follow-up checks:

- Direct generated-project builds for `MSC.Save.Integration`,
  `MSC.Save.Integration.Tests.EditMode` and
  `MSC.UI.Presentation.Runtime` - PASS, no compiler errors or warnings.
- Unity 6000.3.11f1 batch compilation:
  `Logs/M09C_NeedsSlotsFpsMass_Compile.log` — PASS, no C# errors.
- Direct generated-project builds for `MSC.Needs.Runtime`,
  `MSC.Items.Runtime` and `MSC.UI.Presentation.Runtime` — PASS. Existing
  serializer DTO field warnings remain; this batch added no compiler warning.
- Targeted `GameUiRootPlayModeTests`:
  `Logs/M09C_UiPlayModeResults.xml` — PASS, 16/16.
- Phase 1 lighting generator:
  `Logs/Phase1WorldLightingProbeBuilder_SystemFix.log` — PASS, 42 lights and
  6 probes generated and bound to Bootstrap.
- Teimo/streetlight/reflection retune generator:
  `Logs/Phase1WorldLightingProbeBuilder_TeimoOverrides.log` — PASS, 42 lights
  and 6 probes regenerated with the verified Teimo overrides.
- Generated hierarchy readability migration:
  `Logs/DonorWorldHierarchyReadability.log` — PASS, 7,684 generated entity
  instances renamed across 48 scenes without changing runtime identity.
- Targeted lighting/probe/runtime naming EditMode:
  `TestResults/WorldLightingSystemFix_EditMode.xml` — PASS, 5/5.
- Focused UI/GameTime/catalog/Enviro EditMode:
  `TestResults/LightingTimeHud_EditMode.xml` — 59/61 PASS. The two WeatherLab
  scene tests fail before their assertions because the batch/nographics Enviro
  manager has no active `Weather` module. The isolated retry reproduces the same
  pre-existing fixture failure in `TestResults/WeatherLab_Retry.xml`; it is not
  reported as a pass.
- Focused UI settings, GameTime and generated lighting catalog rerun without the
  unrelated WeatherLab fixture:
  `TestResults/LightingTimeHud_Core_EditMode_HDRP.xml` — PASS, 43/43 after
  explicit HDRP light/reflection additional-data binding.
- Updated targeted `GameUiRootPlayModeTests`:
  `TestResults/LightingTimeHud_UI_PlayMode.xml` — PASS, 16/16, including HUD
  corner placement and the persisted FPS toggle.

No full EditMode, full PlayMode or long gameplay suite was run for this batch.

## Remaining 09C scope

- donor-calibrated rates, thresholds, combined modifiers and consequences;
- coffee, cigarette acquisition, calibrated alcohol/smoking/sleep effects and
  injury/death;
- home utilities, cooking/storage, sauna and bathing;
- manual traversal and HUD comparison;
- full 09C parity matrix/test/save evidence.

## Scope correction — 2026-07-26

The vegetation remaster detour is closed. The current trees are conditionally
accepted as a temporary Phase 1 baseline; tree tuning and authored grass
coverage are explicitly deferred to Phase 2. The mesh vegetation painter
remains dormant tooling and is not wired into Bootstrap.

09C resumes with batch `09C-N2`: central needs actions and consequences
(alcohol/hangover, smoking, coffee, urination and sleep), followed by
`09C-H1` domestic utilities, cooking/storage, sauna and bathing. No NPC/10A
work starts before the 09C gate.

## 09C-N2 visible life-action batch — 2026-07-26

This bounded batch adds visible, project-owned Phase 1 feedback without
introducing a physical first-person body or making gameplay depend on animation
frames.

Implemented:

- a lightweight interruptible first-person viewmodel for drinking, eating and
  smoking;
- a visible, time-based urination action with a presentation stream and no
  first-person hands;
- sleep interactions on the player bed, parents' bed and living-room sofa,
  with thin furniture-surface contact patches, camera lowering/tilting and a
  black sleep transition;
- authoritative sleep through the shared game-time advance boundary. Eight
  hours advance time, weather, wetness and needs together instead of directly
  editing the HUD clock;
- authoritative world urination on `P`: urine drains over 2.5–7.5 real seconds
  according to fullness, then dirtiness increases;
- delayed beer effects: thirst reduction, intoxication and urine accumulation;
- provisional cigarette effects: one of 20 units is consumed, stress is reduced
  and fatigue changes slightly;
- faster but still progressive beer absorption, provisional alcohol clearance,
  a persistent hangover state and lightweight intoxication/hangover perception
  feedback;
- native-save schema 3 for hangover and all pending life-effect buffers;
- save-document migration 4 -> 5;
- reset of the needs time cursor after restore, preventing needs progression
  from stalling after loading a save.

The item catalog was regenerated after the effect definitions changed. Beer,
pizza and the sausage package remain reachable at their reviewed home
placements. Cigarettes have a definition and runtime action but deliberately do
not receive an invented home placement: the reviewed world evidence currently
shows them in the Teimo/shop context, whose acquisition flow belongs to the
remaining 09C domestic/service work.

The generated capsule arms and simple action props are temporary
project-owned Phase 1 presentation. They prove action readability and are
removable without changing simulation/save state. Donor-compatible temporary
presentation meshes, clips and routed audio remain follow-up work; these
procedural shapes are not classified `ProductionReady`.

New focused checks:

- Unity batch compilation:
  `Logs/codex_m09c_compile_tests2.log` — PASS;
- 09C needs/life-action EditMode:
  `Logs/codex_m09c_needs_tests.xml` — PASS, 3/3;
- save migration EditMode:
  `Logs/codex_m09c_save_tests.xml` — PASS, 1/1;
- Phase 1 item catalog rebuild:
  `Logs/codex_m09c_item_catalog_build.log` — PASS;
- `git diff --check` — PASS.

Not claimed complete:

- coffee preparation/consumption;
- cigarette acquisition through Teimo;
- calibrated donor coefficients for sleep, cigarettes, alcohol and hangover;
- injury, health and death;
- electricity, water, cooking, storage, sanitation, sauna and bathing;
- final viewmodel geometry, animation and audio;
- the complete 09C parity gate.

The exact next planned batch is `09C-H1`: domestic utilities, cooking/storage,
coffee, sanitation, sauna and bathing. Milestone 10A remains out of scope until
09C is explicitly accepted.

## 09C-N2 post-playtest correction — 2026-07-26

The first manual pass exposed invisible alcohol feedback, instantaneous
urination and a misplaced overhead sleep collider. The correction keeps the
same project-owned simulation/save boundaries and changes only the smallest
compatible runtime presentation and interaction layer:

- one beer's queued intoxication is absorbed over roughly 12 real seconds at
  the current 120-real-minute day rate, rather than remaining imperceptible for
  an extended interval;
- intoxication adds restrained camera sway/vignette feedback; hangover
  presentation and stress/fatigue consequences begin only after alcohol and its
  pending absorption have cleared;
- fatigue uses a lightweight procedural vignette and deterministic eyelid
  closures. BetterMSC was inspected only as a read-only behavioral reference
  for the existence of fatigue/vignette and drunk/alcohol feedback; no mod code,
  asset or runtime dependency was copied;
- urination is an interruptible timed state with progressive bladder drain and
  a stream only. Generated hands are deliberately hidden for this action;
- the ceiling-level sleep collider was removed. Project-owned interaction
  surfaces now cover the player bed, parents' bed and living-room sofa without
  creating a walk-blocking volume above them;
- a development-only console is composed explicitly in Bootstrap. It is
  available only in the Editor or Development Build, opens with backquote or
  `F10`, suspends gameplay input while open and supports reviewed teleport
  anchors, all current needs, position readout and authoritative game-time
  advancement.

Console smoke-test commands:

- `help`;
- `player.position`;
- `player.teleport=list`;
- `player.teleport=teimo` (also `home`, `fleetari`, `inspection`, `landfill`
  and `cottage`);
- `player.needs`;
- `player.needs.fatigue=100`;
- `player.needs.intoxication=50`;
- `player.needs.hangover=40`;
- `time.advance=8`.

Focused correction checks:

- direct generated-project build, `MSC.Needs.Runtime` — PASS, 0 warnings;
- direct generated-project build, `MSC.Bootstrap.Runtime` — PASS, 0 errors;
  28 pre-existing UI DTO serialization warnings remain outside this correction;
- direct generated-project build, `MSC.Needs.Tests.EditMode` — PASS,
  0 warnings;
- Unity 6000.3.11f1 targeted needs/life-action EditMode:
  `Logs/M09C_Needs_EditMode.xml` — PASS, 5/5;
- scoped `git diff --check` — PASS.

The primitive project-owned arms remain temporary Phase 1 readability
presentation. Proper authored viewmodel hands and clips are still a later
presentation pass and cannot become gameplay authority; final production
geometry and animation remain Phase 2 work.

## 09C-N2 fatigue/sleep presentation correction — 2026-07-26

The second manual pass exposed three presentation defects: a temporary food
prop could remain visible after consumption, fatigue feedback looked like a
hard black shutter instead of a blurred vignette, and sleep advanced time
without placing the first-person viewpoint on the selected furniture.

Corrections:

- the temporary held food/drink/smoking prop is now explicitly hidden on
  action start, completion, component disable and teardown;
- fatigue rendering is split from the render-pipeline-agnostic needs runtime.
  `MSC.Needs.Presentation.Runtime` owns a transient HDRP global Volume using a
  quarter-resolution manual Depth of Field pass and a restrained vignette;
- the fatigue Volume blends in from fatigue 62 to 100, blends out smoothly and
  disables itself completely below the active threshold;
- procedural eyelid closures use a bilinear gradient, longer transitions and
  reduced closure instead of hard opaque rectangles;
- each reviewed sleep target queues its own surface position and orientation.
  During the sleep transition gameplay input, the motor and the character
  controller are suspended, the first-person viewpoint is moved onto the
  selected bed or sofa, and the player is restored to the original approach
  position on wake;
- no physical first-person body or animation-frame gameplay authority was
  introduced. Authored viewmodel/body animation remains presentation work.

Focused verification:

- Unity 6000.3.11f1 batch compilation:
  `Logs/M09C_FatigueSleep_Compile.log` — PASS, no C# compile errors;
- targeted needs EditMode:
  `Logs/M09C_FatigueSleep_EditMode.xml` — PASS, 5/5;
- final furniture pose placement, blur strength and eyelid timing still require
  the requested manual first-person playtest.

## 09C-N2 fatigue blink and continuous drinking correction — 2026-07-27

The next manual pass identified two presentation/input defects: fatigue blink
looked like two translucent moving rectangles, and beer still behaved as a
one-shot fake-prop action instead of using the physical bottle.

Corrections:

- the frozen donor `FatigueEyes` timing was inspected read-only. Its 20–60
  second cadence and 1.05-second close/open envelope were reimplemented as a
  deterministic project-owned curve; no donor texture, animation, FSM or code
  is loaded by the remake;
- the temporary GUI eyelid strips were removed. The curve now modulates the
  existing low-cost HDRP fatigue Volume as a soft dynamic vignette while the
  progressive quarter-resolution depth blur remains separate;
- `F` now has an explicit press/hold/release path. It does not pick up a world
  item: LMB remains the pickup/release control, RMB remains throw and the wheel
  remains rotation;
- holding `F` on an already carried liquid consumable drinks at the provisional
  rate of 0.22 litres per real second. Releasing `F`, dropping, throwing,
  disabling input or depleting the liquid interrupts the action safely;
- the actually carried `WorldItemInstance` moves into the drinking pose. The
  old temporary cylinder prop is no longer shown for drinking;
- needs effects are proportional to the amount actually swallowed and continue
  through the existing delayed absorption system. Partial liquid content uses
  the existing item save state; no save schema migration was required;
- an empty retained bottle remains physically held instead of being
  automatically thrown.

Focused verification:

- Unity 6000.3.11f1 batch compilation:
  `Logs/M09C_DynamicDrinkBlink_Compile.log` — PASS, no C# compile errors;
- complete focused Items + Needs EditMode assemblies:
  `TestResults/M09C_DynamicDrinkBlink_EditMode.xml` — PASS, 29/29;
- scoped `git diff --check` — PASS.

Manual acceptance still required for bottle-to-mouth pose, perceived drink
speed, blink softness and fatigue strength. Final authored viewmodel hands,
drink clips and routed audio remain later presentation work and cannot become
gameplay authority.

## 09C-N2 Phase 1 hand/viewmodel correction — 2026-07-27

The user approved a temporary donor-presentation exception for Phase 1 and
clarified the bottle contract. This section supersedes only the input,
drinking-rate and viewmodel limitations stated immediately above; the
project-owned needs and item simulation remain authoritative.

Corrections:

- `F` on a usable world-space liquid bottle now performs one explicit,
  preflighted operation: it picks up the real `WorldItemInstance` and begins
  continuous drinking. Releasing `F` stops drinking but leaves the bottle in
  hand. LMB remains ordinary pickup/release, RMB remains throw and the wheel
  remains item rotation;
- empty or otherwise unusable bottles fail before pickup, so the failed action
  does not emit a transient pickup event;
- a 0.33 litre bottle now drains at `0.05625 l/s`, matching the approximately
  `5.866667 s` final key time of the locked donor `drink_rotate` clip. Gameplay
  uses elapsed unscaled time and never depends on an animation event or frame;
- capturing an item save while a sip is in progress flushes the proportional
  pending need dose before cloning state. Partial content and its matching
  effects therefore remain coherent without a save-schema migration;
- the private local Phase 1 presentation is generated under the ignored
  `RuntimeBaseline` boundary. It copies only the reviewed hand mesh, hand
  textures and compatible legacy clips and wraps them in
  `FirstPersonLifeActionViewmodelBinding`;
- donor `MonoBehaviour`s, PlayMaker FSMs, AnimatorControllers, input, gameplay
  logic, bottle/cigarette geometry and donor shaders are excluded. The real
  project-owned bottle remains the visible and persistent drink container;
- reviewed hello, middle-finger, push and fist clips are available through the
  development console (`player.gesture=...`) for bounded Phase 1 presentation
  review without inventing new player-facing bindings.

The generated donor payload remains private, ignored and replaceable through
`presentation.player.viewmodel.phase2`. Phase 2 must replace the temporary hand
geometry, textures and clips without changing item state, save identity or
action timing.

Final automated verification on 2026-07-28:

- Unity 6000.3.11f1 final batch compilation:
  `Logs/M09C_Phase1Viewmodel_FinalCompile4.log` — PASS, no C# compile errors;
- authoritative manifest-driven private payload rebuild:
  `Logs/M09C_Phase1Viewmodel_Build_Final.log` — PASS; 22 reviewed assets and
  19 sanitized clips, with no donor runtime behaviours/controllers;
- mandatory ignored-payload build guard:
  `Logs/M09C_Phase1Viewmodel_BuildGuard.log` — PASS; prefab, copied GUIDs,
  complete binding, generated-asset SHA inventory and manifest-linked build
  report are current;
- focused player interaction:
  `TestResults/M09C_Viewmodel_PlayerInteraction_Final.xml` — PASS, 18/18;
- complete item runtime:
  `TestResults/M09C_Viewmodel_Items_Final.xml` — PASS, 23/23;
- complete needs runtime, including positive lifecycle and incomplete-binding
  rejection checks:
  `TestResults/M09C_Viewmodel_Needs_Final2.xml` — PASS, 9/9;
- focused total: PASS, 50/50;
- the legacy world sanitation test no longer reports any Phase 1 player
  viewmodel path or file-type violation. The same test remains red because
  unrelated pre-existing `TerrainPilot` and `BetterMscMapRemediation`
  experiment payloads are outside its frozen 06B1 allowlist; see
  `TestResults/M09C_Viewmodel_Sanitation.xml`;
- generated source hashes and the prefab exclusion contract were validated by
  `Phase1PlayerViewmodelImporter`. The JSON manifest is now the runtime import
  plan for source paths, hashes, GUIDs and donor transform IDs; the generated
  prefab and build report remain ignored by Git, and player builds fail closed
  when either is missing or stale.

Manual visual acceptance remains intentionally pending for the next session:
hand alignment, bottle-to-mouth pose, clip transitions and interruption,
smoking sequence, gesture framing and absence of duplicate bottle geometry.

## 09C-N3 Two-stage bottle use and exact grip anchor — 2026-07-28

The user replaced the previous atomic `F` pickup-and-drink contract with an
explicit two-stage interaction:

- first `F` on a usable world bottle moves the real item into a persistent
  use-ready hand pose without consuming liquid;
- the next held `F` begins continuous drinking; releasing `F` stops the sip
  while leaving the bottle in the ready pose;
- LMB still releases and RMB still throws the authoritative physical item;
  wheel rotation is disabled while the bottle is ready or drinking;
- interruption and item release clear the transient ready state safely. No
  save-schema migration is required because the ready pose is presentation
  state, while item contents and pending needs effects remain authoritative and
  persistent.

The ignored Phase 1 viewmodel prefab was rebuilt from the manifest. It now
contains an empty `Beer Bottle Grip Anchor` under the reviewed donor
`Drink > Hand` target. The anchor uses donor transform file ID `61293`, local
position `(0.3400203, 0.15354463, -0.04791023)` and local quaternion
`(-0.9828533, -0.0077871177, -0.0037561825, -0.18418667)`. No donor bottle
geometry, input, FSM or gameplay authority was imported.

Focused verification:

- private manifest-driven viewmodel rebuild — PASS;
- `FirstPersonLifeActionViewmodelBindingTests` — PASS, 2/2;
- `PlayerInteractionRuntimeTests` — PASS, 18/18;
- `ItemRuntimeAndSaveTests` — PASS, 16/16;
- focused total — PASS, 36/36.

Manual acceptance remains required for the ready grip, ready-to-drink
transition, bottle-to-mouth alignment, interruption and drop/throw behaviour.

## 09C-N4 Ready-pose and camera-jitter correction — 2026-07-28

The first manual pass of N3 exposed two bounded presentation defects: the bottle
rested near the lowered body pose after the first `F`, and ordinary spring carry
physics made it lag and shake when the camera rotated during drinking.

Corrections:

- the reviewed `drink_rotate` clip is now sampled at `0.5 s` for the persistent
  ready pose. Its `0 s` frame is the donor pre-action/lowered pose;
- drinking continues from the same `0.5 s` sample and retains the calibrated
  total use duration;
- the same real `WorldItemInstance` now enters a transient hard presentation
  hold and is snapped to the animated grip after camera/animation updates. It
  is never cloned or reparented;
- collision and Rigidbody simulation settings are restored centrally on
  release, throw, placement, handoff, interruption, disable and restore;
- standalone beer-bottle presentation no longer inherits the incidental crate
  slot rotation, keeping its visual pivot aligned with its Rigidbody;
- `world.entities` captures the pre-carry world physics flags while carry or
  viewmodel presentation owns the body. A save made in the ready pose or during
  a sip therefore restores as a normally droppable physical item without a
  schema migration.

Focused automated verification:

- `FirstPersonLifeActionViewmodelBindingTests` — PASS, 3/3;
- `PlayerInteractionRuntimeTests` — PASS, 19/19;
- `CurrentDomainSaveIntegrationTests` — PASS, 11/11;
- `ItemRuntimeAndSaveTests` — PASS, 16/16;
- focused total — PASS, 49/49;
- scoped `git diff --check` — PASS.

Manual acceptance remains required for the settled ready grip and camera
rotation during a held sip.

## 09C-N5 Project-owned drink-ready carry pose — 2026-07-28

The user accepted the two-stage input contract but requested a visibly separate
carry pose: the hand must hold the bottle upright in front of the player, then
raise it to the mouth only while `F` is held and lower it again on release.

Corrections:

- the manifest is now schema 2 and owns the ready sample, ready root
  position/rotation, lower-screen entry offset and transition duration;
- the importer fails closed when the generated binding lacks the schema-2
  presentation marker or contains invalid pose values;
- first pickup samples the reviewed donor hand articulation and smoothly enters
  a project-owned upright/front carry pose;
- starting a sip smoothly returns the root to the donor drinking pose and
  continues the existing clip from `0.5 s`;
- releasing `F` reverses the active drinking pose and returns to the identical
  ready pose over `0.26 s`;
- the same authoritative bottle Rigidbody remains attached to the existing
  donor-evidenced grip anchor. No clone, donor bottle mesh, gameplay animation
  event or save-schema change was introduced.

Executed integrity checks:

- Unity 6000.3.11f1 batch compilation:
  `Logs/M09C_DrinkReady_Compile.log` — PASS, exit 0, no C# compile errors;
- authoritative private payload rebuild:
  `Logs/M09C_DrinkReady_Build.log` — PASS, exit 0, schema-2 report and updated
  generated prefab hash;
- ignored-payload build guard:
  `Logs/M09C_DrinkReady_Guard.log` — PASS, exit 0.

Automated gameplay suites were intentionally not rerun for this bounded visual
pass at the user's request. Manual acceptance is deferred to the next session
and is specified in
`Docs/Items/M09C_DRINK_READY_MANUAL_TEST_RU.md`.

### 09C-N5.1 Linked ready-grip orientation correction — 2026-07-29

The first manual N5 pass showed that the complete ready assembly was inverted:
the bottle pointed down and the wrist approached it in the matching inverted
orientation. The project-owned ready root now applies the equivalent additional
180-degree local-X turn. This rotates the hand, forearm and the authoritative
physical bottle together while leaving the reviewed donor drinking animation,
world-item transform, release/throw physics and save state unchanged. The
generated private prefab must be rebuilt from the updated manifest before the
next manual comparison.

### 09C-N5.2 Ready-grip pivot compensation — 2026-07-29

The linked 180-degree ready-grip correction exposed that the donor clip pivots
around the viewmodel binding origin rather than the visible hand. The ready-root
position now applies the deterministic inverse pivot displacement
`(-0.244258, 0.084114, 0.502312)` so the correctly oriented hand and bottle
retain the former camera-space grip point. The drink-entry offset and active
drinking pose are unchanged; this is a presentation-only correction with no
item, physics, interaction or save impact.

### 09C-N5.3 Ready-grip framing correction — 2026-07-29

The linked hand/bottle orientation from N5.2 is retained. A bounds/FOV review
showed that the intermediate camera-local `(-0.08, -0.12, 0.55)` value placed
the grip almost below the camera frustum. The generated binding is therefore
reframed to camera-local `(-0.22, 0.06, 0.54)`, which keeps the complete hand
and upright bottle in the lower-left forward view. Drinking still transitions
to the reviewed donor clip root; only the persistent ready pose is
repositioned.

## 09C-H1a bounded domestic utilities slice — 2026-07-29

After completing the bounded N5 drink-ready presentation pass, work resumed on
the next planned 09C item: domestic utilities and sauna. This is deliberately
an `H1a` foundation, not a claim that the complete `09C-H1` home/life loop is
finished.

Implemented boundaries:

- a project-owned `MSC.Home.Runtime` state owner with stable action IDs and no
  donor hierarchy/name lookup;
- kitchen tap open/drink/close flow. Drinking requires an open tap and queues
  provisional delayed thirst relief plus urine increase through the existing
  needs-effect boundary;
- independent shower switch and water-valve state. Cleaning is applied only
  while both are active and the player is within the explicit local shower
  radius;
- toilet use through the existing timed urination authority and one-step sink
  washing through the needs-effect boundary;
- an electric-sauna foundation with power, 30/60/90-minute timer cycling,
  game-time-driven heating/cooling, a minimum-temperature steam gate and
  decaying steam state;
- independent persistent base-state toggles for the kitchen stove, television
  and fireplace;
- explicit project-owned interaction targets at reviewed home fixture
  coordinates, composed by a dedicated home installer rather than by runtime
  donor-object searches;
- development-only `home.status` and
  `home.action=<stable-action-id>` diagnostics;
- required native-save domain `home.state`, schema `1`, restored in the global
  state phase;
- save document version `6` and migration `5 -> 6`, which supplies a validated
  fresh home-state domain to older accepted development saves.

The implementation does not redesign the accepted 08A UI, replace Player or
Interaction architecture, or make presentation objects authoritative. The
home-state DTO remains independent of streamed visual cell presence.

Not included and not claimed complete:

- final water, steam and fire VFX;
- routed production audio or finished device/fixture animations;
- wood, fuel, utility consumption, resource circuits or fire safety;
- cooking, food spoilage, refrigerated storage or container workflows;
- complete television/fireplace gameplay;
- donor-calibrated needs, heating and timing coefficients.

All H1a coefficients remain explicit provisional project tuning.

Executed verification:

- `MSC.Home.Runtime.csproj` — PASS, 0 warnings / 0 errors;
- `MSC.Save.Integration.csproj` — PASS, 0 warnings / 0 errors;
- Unity 6000.3.11f1 batch compilation —
  `Logs/M09C_H1_Compile.log`, PASS, exit 0, no C# errors or warnings;
- `HomeSaveMigrationTests` — PASS, 3/3,
  `TestResults/M09C_H1_HomeSave_EditMode.xml`;
- parity and save-coverage CSV parse — PASS, with no duplicate IDs;
- repository-wide `git diff --check` — PASS.

No gameplay/manual pass is claimed. Manual acceptance is deferred to the next
session and is specified in
`Docs/Home/M09C_H1_DOMESTIC_SLICE_MANUAL_TEST_RU.md`.

The next step remains completion and evidence-backed acceptance of the
remaining `09C-H1` domestic scope. Milestone 10A remains blocked until the full
09C gate is explicitly accepted.

## 09C-H1b domestic fluid presentation and donor sauna controls — 2026-07-29

This follow-up extends the accepted H1a architecture without replacing the
Player, Interaction, Needs or Save authorities.

Implemented:

- project-owned procedural fluid presentation for the shower head, shower
  faucet and urination. The particles use gravity, collision, soft alpha
  sprites and collision splashes instead of the former line-renderer stub;
- the shower valve controls water flow, while the separate outlet selector
  switches exclusively between the shower head and faucet. Only shower-head
  flow within the cleaning volume reduces dirtiness;
- the urination action remains time-based and drains the need progressively.
  It now renders a gravity-affected world stream without first-person hands;
- the electric-sauna controls use donor-evidenced Range behaviour:
  heat changes by `15°` and clamps to `1..150°`; timer changes by `10°`,
  clamps to `1..120°`, and maps to `6` seconds per degree (`720` seconds
  maximum);
- mouse-wheel input adjusts the currently targeted sauna control. Held-item
  wheel rotation retains priority and middle mouse still changes the held
  rotation axis. Pressing `F` is not an alternate knob adjustment path;
- `home.state` is now schema `2`; save document version is `7`. Migration
  `6 -> 7` converts the former power/timer representation into validated
  heat/timer angles and preserves the rest of the home state;
- the schema-2 drink-ready manifest and generated prefab now use
  camera-local ready position `(-0.22, 0.06, 0.54)` with rotation
  `(122, -12, -8)`.

Executed verification:

- Unity 6000.3.11f1 batch import/build of the player viewmodel — PASS, exit 0;
- `HomeSystemRuntimeTests` — PASS, 4/4;
- `HomeSaveMigrationTests` — PASS, 9/9;
- no C# compilation errors were reported by either Unity test run.

Manual visual acceptance is still required. The current checklist is
`Docs/Home/M09C_H1B_WATER_SAUNA_MANUAL_TEST_RU.md`. Finished fixture
animations, production-routed audio, steam/fire presentation and final fluid
art tuning remain outside this bounded pass.

### 09C-H1b fluid-rendering correction — 2026-07-31

- the procedural stream material is now configured and validated through the
  HDRP material API instead of relying on incompatible manually assigned blend
  properties;
- shower, faucet and urination particles use short translucent streaks with
  distance sorting, reduced velocity stretching and profile-specific size,
  lifetime and turbulence. This removes the opaque black cards/needles seen in
  the first visual pass;
- the shower and faucet anchors no longer use the centres of the donor
  `WaterTap` capsule volumes. They use the physical outlet endpoints: the
  shower-head stream starts at the upper nozzle and follows its downward angle,
  while the faucet stream starts at the end of the lower spout;
- no third-party fluid asset was added. The correction remains project-owned
  and preserves the existing Home, Needs, Player and Interaction authorities.

Executed verification:

- `dotnet build MSC.Presentation.Fluid.Tests.EditMode.csproj` — PASS,
  zero warnings and zero errors;
- `dotnet build MSC.Bootstrap.Runtime.csproj` — PASS, zero errors; existing
  unrelated DTO-field warnings remain;
- Unity Editor log tail contained no C# compilation failure after the project
  refresh.

The particle appearance was manually accepted by the user. The corrected
outlet positions still require a short in-Editor visual confirmation.

## Milestone closure — 2026-07-31

The user completed the current manual 09C pass and explicitly confirmed:
`я проверил 09С - там все ок, на первое время подтверждаю закрытие`.

Milestone 09C is therefore closed as the accepted Phase 1 baseline. This
acceptance does not reclassify temporary donor presentation as production art
and does not erase the documented calibration, audio, animation or polish debt.
The dependency gate for Milestone 10A is cleared.

## Post-closure licensed first-person hands overlay — 2026-08-10

This bounded presentation follow-up preserves the accepted 09C Player,
Interaction, Needs, item and save authorities.

Implemented:

- selectively imported the user-purchased `Realistic FPS Hands v1.0` Generic
  rig and four 4K maps from package SHA-256
  `BD4B657AD2C2817C0ED83DECA2EC4A00FA16802E428FF2E79F4AFEB6844F8A5B`;
- generated a project-owned HDRP material and interruptible drink, wave and
  middle-finger clips. Vendor controllers, scripts, demo content, legacy
  shaders, accessories, blood variants and source archive remain excluded;
- replaced only drink, wave and middle-finger presentation. Existing donor
  smoke, push and fist presentation remains intact;
- bound wave to `H` / gamepad D-pad Up and middle finger to `M` / gamepad
  D-pad Right through the canonical Player input map;
- retained the real carried beer bottle as item/physics/save authority. The
  animated hand grip drives that object without cloning or gameplay events;
- corrected the rejected first visual pass: the donor beer mesh is Z-long,
  not Y-long; palm/dorsal orientation is now authored independently for the
  two gesture recipients; sip and gestures were moved farther from the camera;
- added a manual acceptance checklist at
  `Docs/Items/REALISTIC_FPS_HANDS_MANUAL_TEST_RU.md`.

Executed verification:

- deterministic Unity player-viewmodel rebuild — PASS, exit 0;
- licensed-hand authoring regression — PASS, 2/2 (beer local-Z grip axis,
  bottle-to-palm distance and recipient-facing gesture sides);
- viewmodel binding tests — PASS, 3/3;
- canonical input source contract — PASS, 1/1;
- Player interaction PlayMode flow — PASS, 5/5;
- generated 16:9 review frames for ready, sip, wave and middle finger — PASS.

The first in-game visual pass was explicitly rejected by the user as a
backwards, oversized mutant. That pass is superseded by the corrected generated
payload and pose regressions above. Final in-game camera/material acceptance of
the corrected payload remains pending; this does not change save schemas,
stable IDs or gameplay timing.

### Licensed-hand anatomical reauthoring — 2026-08-11

The subsequent donor-trajectory transfer was also rejected after in-game
screenshots exposed a detached bottle grip, undersized wave and deformed
fingers. It is superseded by project-authored camera-local arm profiles,
licensed-length arm solving with distributed forearm roll, separate anatomical
finger poses, a palm-local cylindrical bottle grip and a generated
hand-and-forearm-only right viewmodel mesh. The locked donor clips now provide
durations only. Review rendering
uses the gameplay FOV of 72 degrees. A later pose-lab pass tucked the
middle-finger thumb across and into the palm, replaced uniform neighbouring
finger arcs with per-joint curls and turned the palm toward the recipient. The
sip now rotates 75 degrees in the correct direction: the bottle base rises
toward the viewer while the cap moves away toward the camera-local mouth. Wave
travel moves the elbow and forearm with only a small secondary wrist angle.
Upper-arm/shoulder-dominated mesh triangles and camera-local motion vectors are
excluded from the generated viewmodel.

Focused licensed-hand authoring verification passes 5/5. It locks the sip axis,
short-drink return, lower-right bent elbow, whole-arm wave, palm-facing
middle-finger silhouette, palm-local cylindrical grip, hand-and-forearm-only
mesh and disabled viewmodel motion vectors. Final in-game visual acceptance
remains pending.

This correction changes presentation only. Player input, carried-item physics,
needs, saves, stable IDs and the accepted 09C gameplay authority are unchanged.
Final in-game visual acceptance remains pending.

### AXIS full-action runtime-evaluation correction — 2026-08-13

The active viewmodel now uses licensed AXIS right-hand roots for drink, smoke,
wave and push, plus left-hand roots for middle finger and thumbs-up. The
previous hybrid donor hand roots remain provenance only. Executed PlayMode
comparison isolated the catastrophic in-game deformation to Unity 6 legacy
`Animation.Play`; the same clips are finite and anatomically bounded through
direct `AnimationClip.SampleAnimation`. The binding now uses that correct path
behind an interruptible project-owned clock. The smoke prop follows a finger
bone anchor, and the beer grip maps the donor bottle's local `-Z` neck toward
the mouth. This remains presentation-only and does not change Player,
Interaction, Needs, item, stable-ID or save authority.

Post-correction Unity 6000.3.11f1 validation passed the six-action realtime
HDRP PlayMode audit `1/1`, the complete ready/mouth/four-sip/return drink
timeline `1/1`, and the generated binding EditMode fixture `4/4`. These checks
exercise the direct runtime sampling path and the real imported beer mesh, not
only static FBX inspection. Subjective acceptance in the populated game camera
remains manual.

The expanded AXIS authoring regression also passes `6/6`: all fourteen clips
are event-free 60 Hz legacy containers, every gesture owns the intended side,
the licensed eight-weight skinning is retained, and sampled meshes stay inside
the camera-local envelope without torn triangles.
