/plan

# MILESTONE 10 — VERTICAL-SLICE OPTIMIZATION, CONTENT AUDIT, AND BUILD

Read `AGENTS.md` completely.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

Read:

- all milestone reports through 09;
- latest reviews;
- performance budget;
- world/vehicle/weather/Enviro/audio/UI/save performance reports;
- Enviro vendor-boundary and environment-owner reports;
- donor runtime baseline revision/validation reports;
- production-override fidelity reports and human approval metadata where such overrides are included;
- build and source-control policy;
- current Git status and diff.

## Objective

Measure, optimize, validate, and produce a Windows x64 development build of the
current vertical slice.

Optimize only measured bottlenecks.

Do not perform speculative architecture rewrites.

## World-baseline and production-override build gate

A validated sanitized donor runtime baseline is allowed in an explicitly private
local feature-parity development build.

Do not produce a candidate build when:

- inactive prototype-rejected custom visual roots are enabled;
- the donor baseline revision is missing or fails validation;
- forbidden donor scripts/FSM/runtime assemblies are present;
- reference-only content leaks into the build;
- gameplay depends on donor hierarchy names/paths;
- a production override is included as complete without donor comparison and
  explicit human approval.

The build audit must distinguish:

- `ReferenceOnly` — never included;
- `TemporaryDirectImport` donor runtime baseline — allowed only in the explicit
  private feature-parity build profile;
- `ProductionOverride` — allowed when technically validated; visual-complete
  status requires human approval;
- distributable/public profile — donor baseline excluded unless explicit rights
  allow it.

Do not claim the temporary donor baseline is final remastered art.

## Vertical-slice definition

Before optimizing, document the exact current slice:

- starting scene;
- required world zones;
- player actions;
- vehicle assembly actions;
- vehicle drive route;
- weather states;
- audio path;
- UI flow;
- save/load flow;
- expected duration;
- known missing content.

Create:

`Docs/Build/VERTICAL_SLICE_DEFINITION.md`

Do not claim a full-game build.

## Reproducible profiling setup

Record:

- hardware;
- OS;
- Unity version;
- render pipeline;
- resolution;
- graphics preset;
- VSync/frame cap;
- build/editor mode;
- profiler attachment;
- development-build options;
- weather/time;
- world route;
- vehicle configuration;
- save file;
- warm-up procedure.

Use representative builds where possible, not Editor-only conclusions.

## Profile domains

### CPU

- main thread;
- render thread;
- scripts;
- physics;
- vehicle simulation;
- world streaming;
- UI;
- audio;
- save/load;
- garbage collection;
- job-system usage;
- spikes.

### GPU

- shadows;
- Enviro volumetrics/fog;
- Enviro volumetric/flat clouds;
- transparency;
- vegetation;
- water;
- post-processing;
- reflections and Enviro-triggered refresh cadence;
- decals;
- overdraw;
- resolution scaling.

### Memory

- total;
- textures;
- meshes;
- audio;
- managed heap;
- native allocations;
- world-cell load peaks;
- duplicate assets;
- leaks across load/unload.

### Content

- draw calls;
- batches;
- visible renderer count;
- triangles;
- material count;
- shader variants;
- LOD behavior;
- HLOD/proxy behavior;
- collider count;
- Rigidbody count;
- active audio voices;
- UI rebuilds.

### I/O and loading

- boot;
- main menu;
- new/load game;
- world cell;
- save;
- scene transition;
- SoundBank load;
- addressable/resource load when used.

## Optimization rules

For every optimization:

- identify measured problem;
- capture baseline;
- state hypothesis;
- make smallest change;
- capture result;
- record visual/behavioral cost;
- retain or revert based on evidence.

Do not lower quality globally to hide one bad asset.

Do not disable correctness checks merely to improve a metric.

## Likely validation areas

Inspect and optimize when measured:

- world cell size and loading radius;
- HLOD/LOD;
- vegetation density/culling;
- shadow distance/casters;
- Enviro module/quality configuration;
- custom post-process registration and empty/inactive overhead;
- duplicate sky/cloud/fog/light owners;
- volumetric quality;
- rain/puddle/lightning cost;
- reflection update cadence/spikes;
- water;
- material variants;
- texture sizes/streaming;
- vehicle substeps;
- collider complexity;
- per-frame allocations;
- event subscription leaks;
- UI update frequency;
- audio emitter/occlusion budget;
- save serialization spikes;
- debug systems accidentally enabled.

## Quality presets

Create or validate scalable presets:

- Low;
- Medium;
- High;
- Ultra or Custom when justified.

Presets must map coherently across:

- rendering;
- shadows;
- reflections;
- volumetrics;
- vegetation;
- weather through project-owned quality bindings to Enviro;
- water;
- post-processing;
- LOD;
- view distance;
- UI effects.

Do not expose unsupported settings.

## Build-content audit

Create a strict audit for:

- donor/reference meshes in production build;
- donor/reference textures;
- raw extraction data;
- decompiled source;
- Editor assemblies;
- debug scenes;
- development-only cameras/UI;
- unused scenes;
- missing licenses/notices;
- generated Wwise caches/banks policy;
- Enviro demo scenes/sample content accidentally included in Build Settings;
- WeatherLab or other development scenes included in shipping;
- Azure Sky or duplicate environment packages active in production;
- direct gameplay/core references to Enviro vendor types;
- modified Enviro vendor files;
- absolute local paths;
- secrets;
- duplicate stable IDs;
- missing scripts;
- missing materials;
- missing addressable/resource references.

Create:

`Docs/Milestones/BUILD_CONTENT_AUDIT.md`

A failed donor/reference audit is a build blocker.

## Automated checks

Run:

- compilation;
- EditMode tests;
- PlayMode tests;
- project validation;
- world validation;
- Enviro integration/environment-owner validation;
- vehicle validation smoke tests;
- save round trip;
- UI flow smoke tests;
- build-content audit;
- Windows x64 development build.

If a test cannot run, report it explicitly.

## Vertical-slice playthrough

Create and execute a checklist covering:

1. boot;
2. main menu;
3. start/load;
4. spawn;
5. movement;
6. interaction;
7. vehicle part install/fasten;
8. engine start;
9. drive;
10. clear/rain/storm weather transition and wetness response;
11. audio;
12. pause/settings;
13. save;
14. quit/relaunch;
15. load and verify state;
16. complete route;
17. recover from expected failure cases.

Record every issue with stable ID, severity, reproduction steps, logs, and
screenshots paths.

## Build

Produce a Windows x64 development build when possible.

Record:

- output path;
- commit hash;
- build timestamp;
- scenes;
- build options;
- test status;
- known issues;
- content-audit result;
- file size;
- startup result.

Do not present an unlaunched build as verified.

## Output

Create or update:

- `Docs/Build/VERTICAL_SLICE_DEFINITION.md`;
- `Docs/Build/PROFILING_BASELINE.md`;
- `Docs/Build/OPTIMIZATION_CHANGE_LOG.csv`;
- `Docs/Build/QUALITY_PRESETS.md`;
- `Docs/Build/ENVIRO3_BUILD_AND_PERFORMANCE_AUDIT.md`;
- `Docs/Build/WORLD_CELL_FIDELITY_BUILD_GATE.md`;
- `Docs/Build/BUILD_REPORT.md`;
- `Docs/Build/PLAYTHROUGH_REPORT.md`;
- `Docs/Milestones/BUILD_CONTENT_AUDIT.md`;
- `Docs/Milestones/VERTICAL_SLICE_KNOWN_ISSUES.md`;
- `Docs/Milestones/MILESTONE_10_REPORT.md`.

## Definition of done

1. Slice scope is explicit.
2. Baseline is measured.
3. Changes are evidence-driven.
4. Major regressions are fixed or documented.
5. Tests are run or explicitly unavailable.
6. Content audit passes.
7. A Windows x64 development build is produced when tooling permits.
8. The build is launched and smoke-tested when possible.
9. Known issues are stable and prioritized.
10. No donor/reference leakage is hidden.
11. Enviro vendor files are unchanged, WeatherLab/demo scenes are excluded, and only one environment owner is active.
12. Included production world cells have required donor-fidelity evidence and human approval.

## Final response

Report:

1. Slice definition.
2. Hardware/settings.
3. Baseline.
4. Bottlenecks.
5. Optimizations and deltas.
6. Quality presets.
7. Test results.
8. Content audit.
9. Build path and launch result.
10. Playthrough results.
11. Known issues.
12. Files changed.
13. Remaining blockers.
14. Readiness for final polish.

Stop after Milestone 10.
