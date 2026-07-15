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

Deliver:

- time of day;
- clear/overcast/rain;
- wetness and puddle prototype;
- fog/wind;
- save/load;
- performance capture.

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
