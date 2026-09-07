# Phase 1 execution plan

Статус: **Phase 1 gate not met; active closure boundary is 11A-V1 full Satsuma
state (audit snapshot 2026-09-02)**.

## 2026-09-02 execution audit

- 09A native-save foundation exists at document version 16 with 14 current
  participants. Core storage, slots, recovery and migrations are
  `ImplementedUnverified`; full-domain unloaded-cell and fresh/mid/late
  coverage remains Partial.
- 09B/09C item, needs and home foundations are present, but no corresponding
  domain is donor-parity complete.
- 10A foundation is accepted. Current NPC roster coverage is 21 Partial and 51
  Evidence-only required rows; 10B is not complete.
- 11A-V1 is active. Last-green Satsuma builder evidence for `11A-V1d.48` has
  125 loose parts, 117 mounts, 260 fasteners and a `29/29` generated-content
  pass. Later current-tree edits fail compilation on three missing builder
  helper references; wiring, fluids, thermal, wear/damage, tuning/electrical,
  inspection/Fleetari and full gameplay comparison remain open.
- 11B traffic foundations exist for all ten traffic rows, but every row remains
  Partial pending complete route/schedule/feedback/manual comparison.
- 12A economy is Partial; Teimo/pub/fuel/Fleetari services are Partial; only the
  catalog-order communications row is Partial. Remaining services and
  communications are Evidence-only.
- 12B Jobs, 13A Story, 13B Authority/Rally and 13C Media have no complete
  runtime domain implementation and must not be skipped because later
  foundations exist.
- 14A–15C gates are not active and cannot be used to bypass unfinished owner
  milestones.

Detailed status and evidence:
`Docs/Reviews/FULL_GAME_PARITY_AUDIT_2026-09-02.md`.

Порядок milestones сохраняется. `OwnerMilestone` в parity matrix является
авторитетным отображением каждой required row; диапазоны ниже раскрывают
bounded batches, зависимости и минимальное acceptance evidence.

## 09A — Full Game Native Save Foundation

### 09A-S1: storage, slots и versioned document

Rows: `P1.SAVE.001–P1.SAVE.006`, UI New/Continue/Load bindings.

- project-owned save document, atomic storage, slots, backup/quarantine;
- schema version, migration registry и transaction-safe apply;
- explicit failure reports без повреждения исходника.

Evidence: EditMode corruption/migration/round-trip suite, PlayMode New/Load,
disk artifact inspection, UI capability truth.

### 09A-S2: aggregate state и unloaded cells

Rows: save dependencies всех доменов; concrete entity-ID provider.

Evidence: fresh/mid/late fixtures, unloaded-cell mutation, replacement-key
round-trip, duplicate-ID and missing-entity reports.

### 09A-S3: donor save evidence decision

Read-only capture точной donor schema. Importer реализуется только после
evidence и отдельного user decision; отсутствие importer не отменяет native save.

## 09B — World Items, Consumables and Containers

### 09B-I1: definitions, instances и physical persistence

Rows: `P1.ITEM.*` identity/placement/carry/recovery categories.

Evidence: registry completeness against item roster, spawn/pickup/place/throw,
cell unload/reload and save round trips, no name-based runtime lookup.

### 09B-I2: food, drink, ingredients, fluids and packaging

Evidence: quantity/container state, consume/open/pour/fill transitions, needs
outputs, empty/full variants, audio/UI feedback and save coverage.

### 09B-I3: tools, service supplies and exceptional recovery

Evidence: correct capability/tool gating, unique-object recovery without
duplication, out-of-bounds and corrupted-state tests.

## 09C — Player Needs, Home, Sauna and Life Loop

### 09C-L1: Player Locomotion Parity — первый bounded batch

Rows: `P1.PLAYER.001–P1.PLAYER.009`, `P1.PLAYER.014–P1.PLAYER.015`.

1. donor measurements for walk/run, standing/crouch/deep-crouch, forward
   on-foot lean and bilateral vehicle lean through an open window/door;
2. exact home/pub/Teimo/Fleetari stair and threshold fixtures by project stable
   anchors;
3. collider audit before motor changes;
4. compatible CharacterController tuning plus bounded step/ground-snap solver;
5. backend migration only if the correct-collider suite still fails objective
   frame-rate/reload criteria.

Evidence: posture/headroom transitions, speed tolerances, obstruction-aware
lean, pub and Teimo routes, frame-rate sweep, cell reload, pause/input reset,
interaction ray/carry, footsteps and OOB recovery regressions.

### 09C-N1: needs simulation

Rows: `P1.NEEDS.*`.

Evidence: donor-calibrated rate fixtures, combined modifiers, death thresholds,
UI/audio bindings and full save round-trip.

### 09C-H1: domestic life and sauna

Rows: `P1.HOME.*`.

Evidence: home utilities, sleep, washing, sauna heating/steam/fire safety,
resource consumption and persisted world state.

## 10A — NPC Character Runtime Foundation

### 10A-N1: definitions, instances and presentation wrappers

Rows: shared NPC runtime dependencies for `P1.NPC.*`.

Evidence: stable-ID registry, deterministic lifecycle independent of loaded
cell, temporary presentation replacement, animation/audio boundaries.

### 10A-N2: schedules, dialogue and relationship primitives

Evidence: deterministic calendar/schedule fixtures, interruption/reload,
dialogue conditions, relationship persistence and no donor FSM runtime.

Implementation evidence (2026-08-01):

- `MSC.Characters.Runtime` and `MSC.NPC.Runtime` own definitions, stable
  instances, anchors, schedules, deterministic off-screen route progression,
  dialogue conditions/hooks, flags and relationships;
- `npc.state` schema 1 is a required production domain in native save document
  v8; migration `7 -> 8`, semantic preflight, rollback and fixture round-trip are
  covered;
- three explicit framework-only fixtures exercise stationary-service,
  scheduled-roaming and vehicle-linked patterns. Their routes/schedules are not
  donor calibration and do not verify the corresponding 10B roster rows;
- the manifest-driven Editor importer reconstructs only the reviewed mesh/bone/
  clip slices in ignored `RuntimeBaseline/Characters`, strips animation events,
  excludes donor scripts/FSMs/controllers/materials/audio and fails builds closed
  when the local payload is missing or stale;
- the first manual pass exposed a completed-at-noon route and per-tick clip
  restart. The corrected roaming fixture visibly ping-pongs and unchanged
  activity no longer restarts its animation;
- the follow-up confirmed movement/streaming but exposed renderer-based culling
  of sibling skeleton/renderer branches. Generated wrappers now require
  `AlwaysAnimate`, and focused validation samples real bone changes in all three
  clips;
- focused NPC runtime/presentation validation now passes `8/8`; the complete
  save regression passes `52/52`, and the real-frame generated animation
  PlayMode smoke passes `1/1` for all three wrappers. The user confirmed Teimo,
  Alpo and Latanen presentation plus streaming transitions and explicitly
  accepted the bounded 10A foundation on 2026-08-01. This does not promote any
  10B donor-parity roster row to `Verified`.

## 10B — Full NPC Roster, Dialogue and Schedule Parity

### 10B-R1: service and relationship NPCs

Rows: roster group `ServiceOrRelationship`: `P1.NPC.001`, `P1.NPC.002`,
`P1.NPC.007`, `P1.NPC.008` (4 rows). This bounded first package does not mean
that the game contains only four NPCs.

### 10B-R2: personal contacts, job clients and individual services

Rows: roster groups `Relationship`, `RelationshipState`, `JobClient`, `Service`,
`AuthorityService`, `Minigame` (14 rows).

### 10B-R3: ambient residents and transport occupants

Rows: roster groups `Ambient`, `Transport`, `TransportGroup`, `StoryTraffic`
(20 rows, including the non-required unresolved `P1.NPC.021` evidence row).

### 10B-R4: event and story individuals

Rows: roster groups `Event`, `EventService`, `EventInstance`, `Story` (17 rows,
including the non-required rejected duplicate `P1.NPC.046`).

### 10B-R5: authority, rally and crowd multiplicity

Rows: roster groups `AuthorityGroup`, `AuthorityInstance`, `EventGroup`,
`CrowdGroup` (20 rows).

The five packages account for every one of the 75 authoritative roster rows:
73 required rows plus two evidence/exclusion rows. Group rows are not one
mannequin each; each is expanded into the donor-evidenced runtime multiplicity
and receives project-owned stable instance IDs.

Every required Phase 1 NPC receives a `TemporaryDirectImport` donor-derived
mesh, original material/texture identity and compatible donor animation behind
separate geometry/material/animation production replacement keys. Phase 2
replaces all three presentation layers without changing gameplay identity,
schedule, dialogue, saves or event IDs.

Evidence for each batch: roster-count audit, spawn/schedule/dialogue comparison,
textured presentation/audio visibility, animation behavior, streaming and
save/load coverage. A batch remains `ImplementedUnverified` until its manual
donor comparison is accepted; no neutral mannequin is an acceptable final
Phase 1 presentation.

## 11A — Full Vehicle Roster and Legacy Presentation

### 11A-V1: full Satsuma state

Rows: `P1.CAR.*`.

Evidence: complete assembly graph, wiring/fluids/tuning/wear/damage, start-drive-
service-inspection flow, temporary full presentation and save round-trip.

### 11A-V2: player-drivable and owned vehicles

Rows: vehicle roster class `PlayerDrivable`.

### 11A-V3: service, AI and scripted vehicles

Rows: remaining `P1.VEHICLE.*` except route behavior owned by 11B.

Evidence: roster completeness, correct role/controls/physics or presenter,
audio, recovery, unloaded-cell persistence and replacement-key coverage.

## 11B — Traffic, Public Transport, Train and Route AI

The physical Jani/Petteri precursor and 11B-R3 durable crash/Suski rescue
integration establish the vehicle, route-projection, audio and optional
`traffic.state` boundaries used below. Their temporary 16:04/player-proximity
start must be replaced by the Satsuma rev challenge after the player car is
available. 11B-T1 now supplies the exact general road catalog, ten persistent
Highway actors and seven physical ambient presentation archetypes; manual
density/long-drive acceptance remains pending.

### 11B-T1: road graph and ambient traffic

Rows: `P1.TRAFFIC.001–P1.TRAFFIC.003` plus the ambient portions of
`P1.TRAFFIC.008–P1.TRAFFIC.010`.

Status: `Implemented / automated validation passed / manual comprehensive
acceptance pending` (report: `Docs/Milestones/MILESTONE_11B_T1_REPORT.md`).

### 11B-T2: bus, train, boat and scripted routes

Rows: `P1.TRAFFIC.004–P1.TRAFFIC.007` plus completion of shared
`P1.TRAFFIC.008–P1.TRAFFIC.010` acceptance.

Status: `Implemented / automated validation passed / manual comprehensive
acceptance pending` (report: `Docs/Milestones/MILESTONE_11B_T2_REPORT.md`).

Evidence: route/schedule comparison, crossings/stops, collision safety,
streaming handoff, deterministic recovery and representative long traversal.

## 12A — Commerce, Phone, Mail, Services and Economy

### 12A-E1: money, prices and transaction foundation

Rows: `P1.ECONOMY.*`.

Status: `Implemented / automated validation passed / manual acceptance
pending` (report: `Docs/Milestones/MILESTONE_12A_E1_REPORT.md`).

Evidence: hash-locked 3000 MK fresh balance, 38 Teimo prices, Thursday 5.4%
additive inflation, atomic/idempotent ledger behavior, save migration and live
08A HUD binding.

### 12A-S1: stores, fuel, pub, workshop and authorities

Rows: `P1.SERVICE.*`.

### 12A-C1: catalog order, physical mail, delivery, phone and bills

Rows: `P1.COMMS.*`.

Implementation order inside the milestone is dependency-driven rather than the
row order in the parity matrix:

1. `12A-C1a` — physical home/Fleetari catalogs, book spreads, selection and the
   final order form;
2. `12A-C1b` — a physical, save-backed envelope plus Teimo's order-box handoff;
3. `12A-C1c` — timed delivery of ordered parts as ordinary project-owned item
   instances, including unloaded-cell persistence;
4. `12A-C1d` — readable incoming mail, phone calls and bills that consume the
   same mail/time/save boundaries.

Ordered vehicle parts are inventory/world items before they are Satsuma
assembly parts. Their purchase, transport, storage and delivery must not depend
on the target vehicle, mount points or installation logic being available.
Milestone 11A may later add compatible part definitions and mount behavior
without migrating the mail-order transaction or its persistent item identity.

Evidence: opening/schedule rules, purchase/service/order/payment flows,
insufficient-funds paths, delivery timing, UI/audio and save rollback.

## 12B — Jobs, Repeatable Activities and World Tasks

Bounded batches follow `JOB_AND_ACTIVITY_ROSTER.csv`:

- `12B-J1`: sewage, firewood, farm and delivery work;
- `12B-J2`: vehicle recovery, towing, repair-linked and advertisement work;
- `12B-J3`: berry, hay, Kilju and remaining repeatable activities.

Evidence: prerequisites, world actions, fail/cancel/repeat rules, payment,
NPC/economy coupling and reload at every state.

## 13A — Story, Relationships, Progression and Event Graph

- `13A-E1`: relationship and conditional encounter chains;
- `13A-E2`: home/family/uncle and vehicle-access progression;
- `13A-E3`: late-game outcomes and open-loop continuation.

Rows: `P1.EVENT.*` not assigned to authority/rally/media.

Evidence: project-owned event graph, donor-condition fixtures, all outcomes,
re-entry/idempotency and save/load at every node.

## 13B — Inspection, Police, Rally, Jail, Hospital and Death

- `13B-A1`: inspection and legal roadworthiness;
- `13B-A2`: police/checkpoints/fines/jail;
- `13B-A3`: hospital, death and permadeath;
- `13B-R1`: rally registration, eligibility, stages, results and consequences.

Rows: `P1.AUTHORITY.*`, `P1.RALLY.*`.

Evidence: success/failure matrices, penalties and recovery, save integrity,
complete rally route/timing and user-verified donor comparison.

## 13C — Media, Minigames and Remaining Donor Mechanics

- `13C-M1`: radio, TV, music import and computer;
- `13C-M2`: gambling, sports, theatre/dance/local events;
- `13C-M3`: remaining world triggers and feedback not owned earlier.

Rows: `P1.MEDIA.*`, residual `P1.WORLD.*`.

Evidence: roster completeness, interaction and schedules, legal local-media
handling, audio/UI presentation and persisted progress/state.

## 14A — Phase 1 Parity Gap Audit and Wave Plan

Read-only audit of every required row after 13C. No row is marked `Verified`
from type existence. Produces exact failed flows, missing presentation/save
coverage and bounded 14B wave approvals.

## 14B — Approved Gap Implementation Wave

Owner for only explicitly discovered residual `P1.GAP.*` rows and matrix rows
whose original owner left executed evidence incomplete. Waves remain ordered,
small and user-approved; they do not introduce remake-only scope.

## Post-row closure gates

14C completes missing Legacy presentation; 14D hardens full-game save coverage;
15A executes full integration/playthrough; 15B performs optimization/content
audit/private build; 15C performs stabilization and RC gate. Они не заменяют
исходного owner 09A–14B в matrix.

2026-09-07 bounded 11A follow-up: Satsuma visual/instrument/steady-idle repairs
are tracked in `SATSUMA_VISUAL_AUDIO_PACKET_2026-09-07.md`. They preserve the
accepted architecture/UI and do not open Phase 2 or close broad CAR parity rows.
The user accepted this bounded packet on 2026-09-07 (“принимаю”); it is no longer
awaiting user acceptance. This is not a claim that every proposed manual check
was executed. Its documented limitations and broader 11A-V1 parity gaps remain
open. Next milestone: continue 11A-V1 full Satsuma state with an evidence audit
of the remaining gaps; no new implementation is authorized by this acceptance.

Later on 2026-09-07 the user explicitly authorized the bounded first-drive
packet, requiring an interior driver-place trigger (not outside-door boarding).
Implementation and executed drive/save evidence are tracked in
`SATSUMA_DRIVER_TRIGGER_PACKET_2026-09-07.md`. The real native-car test reaches
first/second/reverse, hydraulic stop, loaded stall and native reload. Human
road/camera/feel acceptance remains pending; broad CAR parity rows stay open.
The user also identified AutoClutch during verification: the donor option is
evidenced, but this implementation uses manual Shift clutch and does not yet
implement that assistant. It remains an explicit Phase 1 gap, not an approved
permanent difference; see the packet's boundaries.
Final scoped results: 18 new EditMode cases passed, driver PlayMode 6/6,
cockpit/assembly regression 24/24 and actual Bootstrap 1/1. The broader six-
assembly run was 1990 passed / 22 failed / 1 skipped; the packet report retains
its unresolved content/fixture/global-regression findings. No clean full-project
gate is claimed; unrelated content and historical test expectations were not
rewritten to suppress those failures.
Update 2026-09-07 after the user's micro-drive: the repaired car is generally
usable, but handling is excessively grippy/toy-like, especially on dirt, without
convincing skids/wheelspin. Work is paused at the user's explicit request; no
more runtime fixes/tests in this session. Preserve the road repairs. Next scoped
milestone: **11A-V1 surface/handling and part-detachment diagnosis**, not
Phase 2. Start from `Docs/Phase1/SATSUMA_HANDLING_HANDOFF_2026-09-07.md` for
the preserved baseline, hypotheses, evidence and unfinished validation gates.
Latest user clarification: airborne motion exists; both player and car appear
to treat dirt/rough ground like asphalt, and parts twist out of installed poses
instead of detaching correctly. These are reported symptoms, not proven causes.
Additional follow-up: doors can open spontaneously while driving; diagnose
latch state and hinge/attachment physics separately, without assuming a shared
cause with distorted parts. Documentation only; work remains paused.

## Общий acceptance contract

Каждая required row закрывается только при сочетании donor evidence,
project-owned runtime implementation, visible/audible feedback, save coverage
или доказанного non-persistent rationale, automated tests и выполненного human
comparison там, где он указан. `ImplementedUnverified` не является готовностью.
