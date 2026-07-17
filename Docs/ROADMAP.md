# Roadmap and Exit Gates

## Milestone 0 — Bootstrap and donor audit

Deliver:

- validated paths;
- repository/document structure;
- donor filesystem inventory;
- Unity version/file-layout findings;
- managed assembly inventory when tools are available;
- donor audit, system map, porting matrix, and initial ledger;
- no donor modification;
- no mass import.

Exit gate:

- audit documents exist and distinguish observed facts from assumptions;
- staging is external;
- repository remains free of donor binaries.

## Milestone 1 — Unity 6 HDRP foundation

Deliver:

- validated HDRP project;
- module folders and asmdefs;
- bootstrap scene/composition root;
- foundational service interfaces;
- stable-ID infrastructure;
- Editor validation entry point;
- EditMode test assembly;
- README commands.

Exit gate:

- Unity compiles with no errors;
- bootstrap scene runs;
- duplicate-ID test passes;
- runtime assemblies do not reference Editor assemblies.

## Milestone 2 — Controlled donor reference pipeline

Status: **completed on 2026-07-13**. See `Docs/Milestones/MILESTONE_02_REPORT.md`.

Deliver:

- manifest and registry;
- path/hash normalization;
- idempotent import planning;
- one donor environment reference;
- one rebuilt environment replacement;
- one donor part reference;
- one rebuilt production part;
- new HDRP material, collision, LOD;
- comparison scene;
- ledger updates.

Exit gate:

- deleting reference-only assets does not break production prefabs;
- repeated import planning produces no duplicate work.

## Milestone 3 — Garage art prototype

Status: **completed on 2026-07-14**. See `Docs/Milestones/MILESTONE_03_REPORT.md` and `Docs/Performance/MILESTONE_03_GARAGE_CAPTURE.md`.

Deliver:

- garage blockout from donor measurements;
- rebuilt garage shell and representative props;
- short reconstructed road segment;
- HDRP lighting/sky/fog baseline;
- basic vegetation;
- performance capture.

Exit gate:

- recognizable scale/layout;
- new production art only in the playable scene;
- performance within provisional budget.

## Milestone 4 — Player and interaction

Status: **completed on 2026-07-14**. See `Docs/Milestones/MILESTONE_04_REPORT.md`.

Deliver:

- movement/look/crouch;
- interaction query;
- pickup/carry/place/drop;
- basic tool interaction;
- debug visualization;
- tests.

Exit gate:

- stable interaction loop works without object-name logic;
- save-compatible held-object state design exists.

## Milestone 5 — Vehicle assembly

Deliver:

- definitions and instances;
- mount points;
- fasteners and tools;
- assembly graph;
- 12–20 representative parts;
- save round trip;
- validation.

Exit gate:

- install/tighten/save/load cycle works;
- donor pivots/mounts are documented and verified.

## Milestone 6 — Vehicle simulation

Deliver:

- engine, clutch, gearbox, differential, wheel/brake prototype;
- engine start/stall;
- short drive;
- calibration fixtures;
- physics/performance capture.

Exit gate:

- stable controllable prototype;
- calculations covered by tolerance tests;
- no giant monolithic vehicle script.

## Milestone 7 — World and weather

Status: **07A is committed; 07B automated implementation gate passed on
2026-07-17; 07C is the only next milestone.** Milestone 07B remains a
WeatherLab/test implementation and is not a production-world rollout.

Deliver:

- time of day;
- clear/overcast/rain;
- wetness and puddle prototype;
- fog/wind;
- save/load;
- performance capture.

Current evidence:

- 07A Enviro 3 preflight and WeatherLab is fixed in commit `61250e2`;
- project-owned GameTime, deterministic weather fronts, stable environment
  outputs, accumulated wetness, fair gameplay lightning, versioned DTOs and the
  Enviro adapter are implemented for 07B;
- vendor-neutral core suites pass `101/101` (`20` GameTime, `29`
  WeatherDomain, `52` WeatherPresentation), Enviro integration passes `13/13`,
  WeatherLab time-domain integration passes `3/3`, and builder/fresh preflight
  `Final_03` passes;
- callback/scheduler failure and reentrancy are transactionally hardened,
  including all-domain rollback for failed WeatherLab DEV advance;
- the automated Editor PlayMode performance harness passes `1/1` and writes
  `PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`;
- canonical Enviro baseline is `538 / 305967931 /
  8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`;
- 07B manual captures and standalone/real-GPU performance sign-off remain
  `PENDING` and are not represented by the automated Editor harness.

Exit boundary: production-world ownership replacement, rollout and validation
belong only to `Prompts/07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`.

## Milestone 8 — Wwise audio

Deliver:

- official integration pinned;
- backend adapter;
- engine parameters/layers;
- ambience/weather/interior prototype;
- missing-event validation;
- Unity fallback retained where practical.

## Milestone 9 — Save hardening and optional donor-save study

Deliver:

- atomic save;
- migrations;
- backups;
- corruption handling;
- documented donor-save format findings;
- optional conversion prototype only if understood.

## Milestone 10 — Optimization and vertical-slice build

Deliver:

- profiled bottleneck fixes;
- LOD/streaming validation;
- build-content audit;
- Windows x64 build;
- known-issues report;
- vertical-slice playthrough checklist.

## Milestone 04A-Pilot completion update

Status: **completed on 2026-07-14**. See `Docs/Milestones/MILESTONE_04A_REPORT.md`.

Delivered:

- one numerically bounded garage-road acceptance zone and four-entry landmark allow-list;
- read-only inspection bound to exact donor container hashes and object PathIDs;
- external metadata-only staging manifest;
- project-owned serializable layout data with stable IDs and tested coordinate conversion;
- ignored ReferenceOnly comparison scene and dependency-leak validation;
- explicit `Blocked` classification for the combined terrain mesh instead of a full-world export.

Exit gate:

- the seven retained road samples and garage anchor have complete portable provenance;
- ReferenceOnly content can be deleted without breaking production assets or build scenes;
- no complete-map, production terrain or exact road-centerline claim is made.

Milestone 04B reference capture and the subsequent Milestone 5 implementation are tracked in the completion sections below.

## Milestone 04A1 — Full world geometry reference transfer

Status: **completed on 2026-07-14 with manual fidelity review pending**. See `Docs/Milestones/MILESTONE_04A1_REPORT.md` and `Docs/WorldTransfer/`.

Delivered:

- exact-hash external AssetRipper export of the serialized `GAME` world;
- 36 045 placement records and 13 509 geometry records with deterministic project-owned IDs;
- context-filtered 3 842-entity reference world in 49 cells plus global/persistent/bootstrap scenes;
- collider, terrain, road, water, vegetation, interior, landmark, missing and unsupported manifests;
- versioned coordinate conversion, partition/streaming boundary, Editor tooling and development fly camera;
- automated validation and tests while preserving Player/Interaction.

Exit gate:

- full discovered serialized geometry is represented or explicitly classified non-world;
- generated donor reference content is ignored, removable and rebuildable;
- no donor payload enters production folders or normal builds;
- known bounds, topology, semantic and manual-fidelity limitations are machine-readable and documented.

Milestone 04B reference capture and the subsequent Milestone 5 implementation are tracked in the completion sections below.

## Milestone 04B — Reference capture and measurement database

Status: **completed on 2026-07-14 with an explicit P0 capture queue**. See `Docs/Milestones/MILESTONE_04B_REPORT.md`, supplemental `Docs/Milestones/MILESTONE_04B1_VEHICLE_ASSEMBLY_STATIC_CAPTURE_REPORT.md` and `Docs/ReferenceCapture/`.

Delivered:

- versioned schema/dataset `1` / `04B.4` with 40 measurements, 11 behavior records and 35 coverage requirements;
- explicit units, coordinate spaces, source hashes, evidence, confidence, tolerance and derived dependencies;
- separate remake tuning overrides, never stored in donor measurement fields;
- 11 machine-readable calibration fixtures and seven manual category checklists;
- Editor dashboard at `Tools > MSC Remake > Reference Capture` plus batch validation;
- 14 focused EditMode tests, a complete database index, missing-data queue and capture-session log;
- bounded rear-drum evidence: static `0.01 m` candidate/graph, one discrete `0..8` marker, wrench `14`, three clean runtime repetitions and a user-confirmed wheel-installed removal blocker, without M05 implementation.

Exit gate:

- database/tooling validation covers 51 records; latest verification results are recorded in `Docs/TESTING_AND_VALIDATION.md`;
- 20 P0 and 6 P1 requirements remain `Partial`/`Missing` and are reported, not fabricated;
- donor installation and saves remain read-only; no donor payload or raw capture entered Git;
- detailed simulation and world remastering did not start.

Readiness decision: **GO for Milestone 5 in its declared scope**. Both representative assembly requirements are `Covered` and the fixture is `Ready`. Fitted-wheel identity and assembled/curb mass remain `Partial` and cannot be treated as production calibration. The next milestone is `Prompts/05_VEHICLE_ASSEMBLY.md`.

## Milestone 05 — Vehicle assembly completion update

Status: **completed on 2026-07-14**. See `Docs/Milestones/MILESTONE_05_REPORT.md` and `Docs/Vehicle/`.

Delivered:

- immutable definitions and mutable instances for parts, mounts, fasteners and tools;
- deterministic assembly graph with install prerequisites, removal blockers and validation;
- preserved M4 carry/handoff/tool/context boundaries;
- safe loose/held/installed/detached Rigidbody transitions;
- 15-part, 14-mount project-authored representative vehicle scene;
- rear-drum clean-room fixture with one BoltPM, wrench 14, discrete `0..8` stages and installed-wheel blocker;
- versioned part/mount/fastener DTO capture and validated restore;
- Editor builder, validator, gizmos, dependency graph and performance audit;
- 22 focused EditMode and 8 focused PlayMode tests.

Exit gate:

- install/tighten/loosen/remove and save/restore cycles pass automated tests;
- the prototype scene has no donor/reference-only production dependency;
- candidate preview is deterministic and measured allocation-free;
- all approximations and unresolved donor measurements remain explicit.

Readiness decision: **GO for the bounded first world-remaster integration pass (`Prompts/05A_WORLD_REMASTER.md`)**. Vehicle simulation remains a later independent milestone.

## Milestone 05A — bounded world-remaster pilot

Status: **bounded first execution completed on 2026-07-14; manual visual acceptance pending**. See `Docs/Milestones/MILESTONE_05A_REPORT.md` and `Docs/WorldRemaster/`.

Delivered:

- separate production/reference/comparison layers and an Editor dashboard;
- durable registry for all 13,509 world records and status rows for all 51 discovered zone groups;
- deterministic `cell_0_-3` home/garage pilot with terrain, road, ditch, buildings, interior, moving architecture, props, vegetation, collision and LOD;
- M4 player and M05 assembly integration without subsystem rewrites;
- 263 grouped manual-art tasks covering every unassigned record;
- dependency, fit, cell, material, collision and LOD validation plus focused EditMode/PlayMode coverage.

Exit boundary: only 24 records are directly bound (`0.178%` global; `3.577%` in the pilot cell), and no record is marked `Approved`/`Verified`. The remaining map is not called remastered. Continue exactly with `Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md` after the pilot's manual Unity review.

## Milestone 05C — full-map geometry evaluation update

Status: **completed and manually accepted on 2026-07-15**. See
`Docs/Milestones/MILESTONE_05C_REPORT.md`.

Delivered:

- removable reference-only overview for all 3,842 eligible geometry records;
- 2,784 actual meshes and 1,058 explicit bounds fallbacks across 49 cells;
- reconstruction of 1,683 legacy static-batch subsets without production
  dependency on donor content;
- automated geometry validation/captures and user manual sign-off;
- explicit classification of tree-wall-bounded donor voids as source topology,
  not a failed geometry transfer.

## Milestone 05C1 — continuous ground and collision baseline

Status: **bounded Teimo implementation completed and manually accepted on
2026-07-15**. See
`Docs/Milestones/MILESTONE_05C1_REPORT.md`.

Delivered:

- one approved `IntentionalDonorVoid` region split between `cell_-4_0` and
  `cell_-3_0`;
- two deterministic project-owned low-detail meshes and additive scenes;
- exact seam at `x=-1536`, static non-convex collision and stable IDs;
- strict validation of 104 vertices, 100 triangles, 26 seam pairs and 100/100
  collision probes;
- focused EditMode `3/3` and PlayMode `1/1` test passes;
- no expansion into other voids, final terrain art or gameplay systems.

Exit gate: automated checks and the bounded manual checklist are complete. The
next and only next milestone is `Prompts/05B_WORLD_VALIDATION.md`; Milestone 06
remains behind that validation gate.

## Milestone 05B — strict world validation

Status: **completed on 2026-07-15; no formal gate achieved**. See
`Docs/Milestones/MILESTONE_05B_REPORT.md` and `Docs/WorldValidation/`.

Validated facts:

- all three gates were evaluated; `PilotGate`, `VerticalSliceGate` and
  `FullWorldGate` currently fail;
- exact eligible production coverage is 33/3,842 (0.858928%) across 2/49 cells;
- measured bounded spatial fixtures pass, while full terrain/road/water/building
  parity remains explicitly unavailable;
- the static production/build dependency graph is clean (49 seeds, 230 assets,
  482 edges, zero prohibited dependencies);
- production-cell load/unload/reload lifecycle passes, but no production
  focus-driven streaming service is wired;
- bounded collision/clearance fixtures pass, but complete real-player traversal
  and current-world runtime performance evidence are absent.

Readiness decision: **NO-GO for `06_VEHICLE_SIMULATION.md`**. The next and only
next milestone is a bounded **05B.1 PilotGate remediation**: wire production
streaming through Bootstrap, add a real M4 CharacterController traversal fixture,
capture current-world performance, and rerun 05B.

This is the historical 05B verdict. It was superseded by the successful bounded
05B.1 remediation below; the original result remains unchanged for auditability.

## Milestone 05B.1 — PilotGate remediation

Status: **completed on 2026-07-15; `PilotGate` achieved**. See
`Docs/Milestones/MILESTONE_05B1_REPORT.md`.

Delivered:

- production streaming for the two accepted cells through Bootstrap;
- fingerprinted two-cycle load/unload/reload evidence;
- real M4 `CharacterController` traversal over 16 checkpoints / `62.780293 m`;
- bounded current-world Windows x64 performance evidence at four locations;
- rerun canonical world validation with 12 closed and 15 open issues.

Exit decision: **GO for `Prompts/06_VEHICLE_SIMULATION.md`**. Production driving
route parity, streaming-boundary driving, collision material policy
`WORLD-COL-003` and isolated physics CPU remained open inputs for later vehicle
validation.

## Milestone 06 — vehicle simulation implementation update

Status: **bounded milestone completed; automated gate passed on 2026-07-15 and
post-remediation manual drive/audio acceptance was recorded on 2026-07-16**. See `Docs/Milestones/MILESTONE_06_REPORT.md` and
`Docs/Vehicle/`.

Authored:

- separated pure simulation and Unity runtime assemblies;
- explicit engine -> clutch -> gearbox -> final drive/open differential -> one
  configurable driven-wheel pair; the authored config maps it to FL/FR (`0/1`)
  for current FWD, while AWD remains future work;
- start/idle/stall/shutdown, clutch, shifts, braking, steering, basic suspension,
  electrical/fluid/thermal placeholders and typed prerequisites;
- cached adapter from a separate 12-part logical assembly fixture;
- replaceable `IWheelPhysicsBackend` plus four-wheel raycast prototype;
- bounded 100 m paved/gravel/dirt/grass graybox route;
- development telemetry, Editor tools, 18 EditMode and 4 PlayMode test cases;
- optional Editor-only, hash-pinned local Satsuma diagnostic audio behind `IVehicleAudioBackend`, with no donor clip in project content or builds.

Reference boundary: exact derived wheel anchors/wheelbase/tracks are used; `389
kg` is not treated as curb mass; candidate `0.272667 m` radius remains
`NeedsReview`; donor dynamic fixtures remain `Missing`; all dynamics are
`RemakeDesignTarget` / `ProvisionalProjectTuning`.

Automated exit gate: PASS for builder, strict validator, calibration, focused
EditMode `18/18`, focused PlayMode `4/4`, full PlayMode `31/31` and the isolated
performance audit. Full EditMode is `157/160` in `44.4875523 s`; all M06 tests pass and the three
failures are the retained two M3 lighting assertions plus one 04A1 donor-hash
drift. Final stability coverage proves level startup rest without pinning a six-degree incline or re-sleeping a later external wake/impulse. Audio transitions are sampled in `FixedUpdate`, their starter ordering is asserted, and missing local staging remains a silent fallback. The first manual smoke confirmed the core loop and exposed startup creep/view shake; automated remediation passes, and the user accepted the post-remediation drive/audio recheck for the bounded basic prototype on 2026-07-16.
Automated and manual readiness for `Prompts/06A_PHYSICS_VALIDATION.md` is PASS.

## Milestone 06A — physics validation

Status: **automated gate passed and user accepted the bounded prototype baseline
on 2026-07-16; `Accepted / HumanAccepted`**.

Delivered:

- reproducible validation profile, isolated course, strict builder/validator and
  bounded production-world fixture;
- focused EditMode **7/7 PASS** and focused PlayMode **8/8 PASS**;
- durable schema-v4 PhysX evidence plus seven per-fixture telemetry CSV files;
- pure-performance evidence at `1/2/4` substeps:
  `2.571855 / 3.55201 / 5.962805 us/tick`, telemetry `6.04886 us/tick` and
  scripted validation `6.95269 us/tick`, with zero measured allocations;
- real-backend stationary PhysX window with blocking `Physics.Simulate` means
  `0.024990 / 0.023025 ms`, combined means `0.038737 / 0.036251 ms` and zero
  allocations for telemetry consumer off/on;
- production telemetry after a 50-frame warmup: mean root + backend
  `0.038197 ms`, linear p95 `0.0461 ms`, maximum `0.0629 ms`;
- bounded production route of `12.255066 m` to `z=-1024` with four wheel
  contacts, followed by a next-cell-only contact probe at `z=-970`.

The two production pilot cells remain `Rejected` / `NeedsRework` for donor
visual and spatial parity. They are accepted here only as technical
collision/streaming fixtures. The `Physics.Processing` marker was unavailable
in batch mode despite the blocking `Physics.Simulate` wall-clock measurement;
GPU timing and a Windows player 60 FPS acceptance capture remain unavailable
and must not be inferred from the passing Editor evidence.

Gate decision: **automated PASS; human acceptance recorded**. Its historical
next step was the 06B world sequence; that sequence is now closed/frozen as
recorded in `Prompts/CURRENT_STATE.md`.

## Milestone 07A — Enviro 3 preflight and WeatherLab

Status: **implementation committed as `61250e2` on 2026-07-17**.

Delivered:

- hash-identified local Enviro 3.x API and HDRP compatibility audit;
- one isolated WeatherLab, single-owner environment policy and project-neutral
  presentation contract;
- explicit Enviro integration assembly boundary with vendor source kept
  read-only;
- automated rain/fog/lightning/lifecycle checks and a reproducible preflight.

The 07A manual capture/performance backlog remains evidence debt; it is not
silently promoted by the later 07B automated result.

## Milestone 07B — project-owned time/weather domain and Enviro adapter

Status: **automated implementation gate `PASS` on 2026-07-17; manual captures
and standalone/real-GPU performance sign-off `PENDING`**.

Delivered inside WeatherLab/tests only:

- deterministic `GameTimeService` and versioned time state/DTOs;
- transactional time mutation: deterministic callback order, reentrancy guards,
  clock/queue rollback on callback failure and composite WeatherLab rollback;
- seeded weather fronts, transitions, overrides and stable environment outputs;
- accumulated/drying wetness, shelter/exposure mapping and shared material
  wetness/puddle prototype;
- separated ambient presentation lightning and project-owned gameplay strike
  selection, cooldown/fairness and thunder requests;
- stable binding-ID mapping into a single Enviro adapter without core Enviro
  references or saved vendor objects;
- Time and Weather DEV tooling, diagnostics and WeatherLab builder/preflight.

Automated evidence is `101/101` vendor-neutral core tests (`20 + 29 + 52`),
`13/13` Enviro integration tests, `3/3` WeatherLab time integration tests and
`1/1` Editor PlayMode performance-harness test. Builder/fresh preflight
`Logs/M07B_WeatherLabBuilder_Final_03.log` is `PASS`; the harness writes the
automated-only JSON at
`PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`. Manual
captures and standalone/real-GPU sign-off remain `PENDING`. The canonical vendor
baseline is `538` files, `305967931` bytes, fingerprint
`8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`.
All logical values/tuning are `RemakeDesignTarget` / project-authored; no donor
weather code or configuration is claimed as ported.

Readiness decision: **GO only for
`Prompts/07C_PRODUCTION_WEATHER_ROLLOUT_AND_VALIDATION.md`**. Do not start
Milestone 08 before the production rollout is validated.
