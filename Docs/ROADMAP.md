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

No 04B or Milestone 5 implementation has started.
