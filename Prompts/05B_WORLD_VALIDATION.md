/plan

# MILESTONE 05B — WORLD PARITY, COVERAGE, AND INTEGRATION VALIDATION

Read `AGENTS.md` completely before doing anything.

Read:

- all reports through Milestone 05A;
- every `MILESTONE_05A_BATCH_*` report;
- world-transfer data and fidelity reports;
- world-remaster ledgers and zone-status files;
- player/interaction and vehicle-assembly reports;
- testing and performance documentation;
- current Git status and diff.

## Objective

Run a strict validation gate over the world that currently exists.

This milestone validates facts.

It does not create broad new art to hide missing coverage.

It must identify whether the project currently satisfies one of these gates:

- `PilotGate`;
- `VerticalSliceGate`;
- `FullWorldGate`.

Do not claim `FullWorldGate` merely because the pipeline exists.

## Gate definitions

### PilotGate

Requires one representative production zone with:

- terrain;
- road;
- building exterior;
- representative interior;
- props;
- vegetation;
- collision;
- LOD;
- streaming;
- player traversal;
- vehicle clearance where relevant;
- donor/reference parity measurements;
- no runtime donor dependency in production-only mode.

### VerticalSliceGate

Requires a continuous playable route and all world content needed by the current
vertical slice, including:

- starting/home/garage area;
- representative road route;
- at least one destination/service area when required by slice design;
- production terrain/roads/buildings along the route;
- streaming;
- collision;
- weather/material compatibility;
- no blocking missing replacements;
- acceptable measured performance.

### FullWorldGate

Requires:

- all planned world zones accounted for;
- all required production replacements complete or explicitly accepted;
- full road/terrain/building/interior/water/vegetation/infrastructure coverage;
- full stable-ID parity;
- no unreported donor-reference fallback;
- validated streaming and performance across the map.

## Validation principles

- Compare against project-owned durable world-layout data.
- Use donor/reference geometry as a parity fixture, not as production truth.
- Distinguish intentional art changes from accidental spatial drift.
- Distinguish missing data from failed import.
- Distinguish unexecuted tests from passed tests.
- Do not alter gameplay-critical layout to make validation pass.
- Do not relax tolerances silently.

## Required validation domains

### Identity and provenance

- stable world IDs are unique;
- replacement bindings resolve;
- donor provenance resolves;
- production assets have replacement status;
- generated data versions match;
- no random identity regeneration;
- save-facing IDs remain stable.

### Spatial parity

Validate by category:

- world origin and bounds;
- terrain extents/elevations;
- road centerlines, width, and elevation;
- junctions and driveways;
- bridges and culverts;
- shoreline and water level;
- building footprints;
- floor levels;
- door/gate/window pivots;
- stairs and ramps;
- landmark positions;
- interaction anchors;
- garage and vehicle clearances;
- interior/exterior alignment.

Report maximum, average, and percentile deviations where meaningful.

### Coverage

Calculate exact counts and coverage for:

- zones;
- terrain tiles;
- road segments;
- junctions;
- buildings;
- interiors;
- moving architecture;
- water bodies;
- infrastructure;
- vegetation prototypes/placements;
- static props;
- colliders;
- LOD-ready assets;
- approved production replacements;
- donor-reference fallbacks;
- missing/blocked assets.

### Production independence

In production-only mode verify:

- no donor render mesh is active;
- no donor texture is referenced by production materials;
- no Editor-only importer assembly is loaded;
- no external staging path is required at runtime;
- no reference-only scene is included unintentionally;
- production prefabs survive removal of reference-only asset paths.

Do not physically delete source/reference content as the test unless a safe
temporary copy or automated dependency analysis exists.

### Streaming

Validate:

- cell generation determinism;
- load/unload behavior;
- cross-cell roads;
- cross-cell water;
- large-object handling;
- interiors;
- persistent landmarks;
- no orphaned object after unload;
- no duplicate stable ID after reload;
- expected memory recovery;
- no obvious frame stalls beyond documented limits.

### Collision and traversal

Validate:

- player can traverse required paths;
- player is not trapped by replacement collision;
- floors and stairs are continuous;
- doors/gates have correct clearance;
- roads provide continuous vehicle surfaces;
- bridge and driveway transitions are valid;
- no accidental invisible walls;
- no major terrain holes;
- collision layers and physics materials are appropriate.

### Art-system integrity

Validate:

- material standards;
- texture import rules;
- LOD completeness;
- shadow/culling settings;
- vegetation instancing;
- decal usage;
- transparent-material misuse;
- duplicate near-identical materials;
- missing maps;
- invalid bounds;
- obvious floating/embedded assets;
- wetness/weather compatibility flags.

### Performance

Measure representative locations:

- pilot/home zone;
- dense vegetation zone;
- building/interior transition;
- road at driving speed;
- water/shoreline zone;
- most expensive known cell;
- vertical-slice route.

Record hardware, resolution, quality settings, build/editor mode, CPU/GPU frame
time, memory, draw calls, visible renderers, triangles, and streaming spikes
when available.

Do not invent an FPS target result.

## Automated tooling

Create or improve a `WorldValidationDashboard` or equivalent.

Required actions:

- run all world validations;
- validate selected gate;
- validate selected zone;
- export machine-readable results;
- show blocking issues;
- jump to stable ID;
- show reference/production overlay;
- show donor dependency;
- show coverage;
- show performance capture locations;
- open latest report.

Create stable issue IDs such as:

- `WORLD-ID-*`;
- `WORLD-GEO-*`;
- `WORLD-ROAD-*`;
- `WORLD-BLD-*`;
- `WORLD-COL-*`;
- `WORLD-LOD-*`;
- `WORLD-STREAM-*`;
- `WORLD-PERF-*`;
- `WORLD-DONOR-*`.

## Allowed fixes

This milestone may fix only:

- validation-tool defects;
- deterministic generator defects;
- clearly incorrect metadata;
- missing ledger/status updates;
- small alignment defects with verified expected values;
- regressions directly caused by the latest world-remaster batch.

Do not author missing hero assets or broadly remodel zones here.

Record every fix.

## Tests

Add or run tests for:

- stable-ID parity;
- replacement resolution;
- donor dependency detection;
- world bounds;
- landmark tolerance;
- road continuity;
- terrain seams;
- cell determinism;
- cell load/unload;
- collision continuity;
- player traversal fixtures;
- vehicle-clearance fixtures;
- production-only mode;
- missing replacement reporting;
- gate calculation.

## Output

Create:

- `Docs/WorldValidation/WORLD_VALIDATION_SUMMARY.md`;
- `Docs/WorldValidation/WORLD_VALIDATION_ISSUES.csv`;
- `Docs/WorldValidation/WORLD_COVERAGE.csv`;
- `Docs/WorldValidation/SPATIAL_DEVIATION.csv`;
- `Docs/WorldValidation/PRODUCTION_DEPENDENCY_AUDIT.md`;
- `Docs/WorldValidation/STREAMING_VALIDATION.md`;
- `Docs/WorldValidation/TRAVERSAL_AND_CLEARANCE.md`;
- `Docs/WorldValidation/PERFORMANCE_VALIDATION.md`;
- `Docs/Milestones/MILESTONE_05B_REPORT.md`.

## Definition of done

1. The achieved gate is named honestly.
2. Coverage percentages are calculated.
3. Blocking issues have stable IDs.
4. Spatial parity is measured.
5. Production independence is tested.
6. Streaming is tested.
7. traversal and collision are tested.
8. Performance is measured where tooling permits.
9. Unavailable tests are listed explicitly.
10. The report gives a go/no-go for Milestone 06.
11. No broad art scope was smuggled into validation.

## Final response

Report:

1. Gate evaluated.
2. Gate achieved.
3. Coverage by category and zone.
4. Spatial deviations.
5. Production dependency status.
6. Streaming status.
7. Collision/traversal status.
8. Performance results.
9. Blocking issue IDs.
10. Allowed fixes performed.
11. Tests and results.
12. Manual validation required.
13. Go/no-go for `06_VEHICLE_SIMULATION.md`.
14. Exact next action.

Stop after Milestone 05B.
