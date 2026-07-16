/plan

# MILESTONE 07C — PRODUCTION WEATHER ROLLOUT, STREAMING, AND VALIDATION

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Milestones/MILESTONE_06B3_REPORT.md`;
- `Docs/WorldBaseline/BASELINE_REVISION.json`;
- `Docs/WorldBaseline/WEATHER_HANDOFF.md`;
- active donor-baseline world profile and legacy-material coverage report;
- all reports through Milestone 07B;
- Enviro installation/API/binding/owner/fog/water reports;
- WeatherLab captures and performance data;
- production bootstrap, additive scene/cell lifecycle, world streaming, road,
  material, vegetation, water, vehicle, save, audio, UI, graphics and quality
  contracts;
- current Git status and diff.

## Entry gate

Stop if:

- 07A or 07B is not closed;
- the project-owned weather domain is not passing core tests;
- vendor source was modified;
- duplicate environment ownership is unresolved;
- the active world profile is not the validated donor runtime baseline;
- inactive prototype-rejected visual roots are still enabled;
- forbidden donor scripts/components exist in the runtime baseline;
- a broad unrelated diff is present.

Do not use weather integration to remaster, redesign, or conceal known donor-baseline visual debt.

## Objective

Roll the validated project-owned time/weather/wetness/lightning domain and
Enviro 3 presentation adapter into the approved bootstrap and active donor-runtime-baseline world, make it safe across additive streaming and save/restore,
add coherent quality tiers, and perform full production validation.

This milestone is production integration and validation, not a new weather
architecture rewrite.

## Production ownership migration

Apply the 07A owner matrix to production scenes.

Requirements:

- exactly one active sky owner;
- exactly one cloud owner;
- exactly one fog owner;
- exactly one sun and one moon/light strategy;
- exactly one time authority;
- exactly one weather authority;
- no Enviro autonomous schedule competing with project state;
- no Azure Sky production owner;
- no duplicate HDRP cloud/fog stack;
- Enviro audio disabled in production;
- reversible cleanup of obsolete temporary placeholders;
- no vendor file modifications.

Create automated detection for duplicate managers/owners across additive scene
loads.

## Bootstrap and lifecycle

Integrate the environment stack through the existing bootstrap/service lifetime.

Validate:

- new game;
- load game;
- additive world-cell load/unload;
- return to main menu;
- scene reload;
- development scene;
- missing/disabled module reporting;
- no duplicate persistent manager;
- no stale subscriptions;
- no sky/fog flash during transitions;
- no weather state reset when a world cell streams;
- clean teardown.

The authoritative domain must survive world-cell changes without living inside a
particular production cell.

## Production binding

Bind the active donor-baseline world and any approved production overrides to project-owned outputs.

### Time and lighting

- project time drives Enviro;
- sunrise/sunset behavior follows documented reference/tuning;
- exposure remains readable in garage/interiors/road/night;
- no crushed blacks or cinematic grading that hides interaction targets;
- reflection/environment updates are thresholded/cadenced;
- no full reflection refresh every frame.

### Weather presentation

Validate production mappings for:

- clear;
- partly cloudy;
- overcast;
- drizzle;
- steady rain;
- heavy rain;
- thunderstorm;
- morning mist/fog;
- night.

Profiles remain project-owned IDs mapped through the binding matrix.

### Wetness and puddles

Integrate global project-owned parameters with supported material families. The temporary donor baseline may have partial coverage:

- terrain/ground;
- asphalt;
- gravel/dirt;
- building exteriors;
- selected props;
- vehicle exterior hooks;
- vegetation wetness hooks;
- bounded puddle/ripple masks;
- shelter/interior exclusion.

Do not instantiate unique materials per object.

Do not require the Enviro Terrain Shader add-on.

Do not mass-reauthor donor materials during this milestone. Record unsupported legacy material families and use a bounded fallback where safe.

Do not implement final wet-road tire calibration here. Preserve the 06A dry
baseline and expose a clearly disabled/tunable wet-friction hook for a future
calibration task.

### Wind

Drive approved vegetation/wind systems through shared project outputs.

Validate:

- no per-tree/per-object Update explosion;
- consistent tree/grass direction and scale;
- restrained motion in normal weather;
- stronger but plausible storm response;
- quality scaling;
- no duplicate Wind Zone ownership.

### Lightning

Integrate:

- ambient Enviro visual lightning;
- project-owned gameplay strike requests;
- world-space presentation;
- listener-distance thunder request;
- spawn/load cooldown;
- protection volumes/attractors in approved cells where justified;
- non-lethal mode when no existing damage contract is available.

Enviro visual events must not bypass the project gameplay director.

Do not add forest fires or full electrical-grid simulation.

## Shelter and interior/exterior behavior

Use one project-owned exposure/shelter contract.

Validate:

- rain particles do not appear inside sealed rooms;
- exterior rain remains visible through windows/doors;
- audio receives interior/exterior intensity outputs, not direct Enviro events;
- wetness does not accumulate on protected interior surfaces;
- open garage doors/porches behave consistently;
- fog does not fill interiors incorrectly;
- transitions do not pop at cell boundaries.

## Water compatibility

Apply the 07A-approved fog/water strategy.

Validate current production water/shore proxies only to the extent they exist:

- horizon and shoreline readability;
- fog over water;
- transparent water/windows;
- rain impact presentation where supported;
- no duplicated reflection/fog owner;
- no vendor shader patch.

Do not build the future full water/swimming/buoyancy system here.

## Donor-fidelity protection

Before weather integration, capture neutral clear-day canonical views for representative donor-baseline routes/cells and every active production override cell.

After integration, repeat the same neutral clear-day captures with matched:

- camera;
- FOV;
- time;
- weather state;
- exposure/reference mode.

Weather integration must not silently:

- move world geometry;
- alter cell placement;
- replace landmark prefabs;
- change terrain/road transforms;
- hide unresolved mismatches with fog/vegetation;
- reactivate the rejected custom visual cells;
- alter the frozen donor-baseline revision or world transforms.

Create a geometry/transform diff and side-by-side regression package.

AI-generated concept art is not a fidelity reference.

## Quality tiers

Create coherent project-owned quality tiers mapped through the adapter.

At minimum support practical Low/Medium/High and optional Ultra/Custom where the
installed Enviro version supports them.

Map only validated settings:

- volumetric vs cheaper cloud strategy;
- cloud steps/resolution/distance;
- fog quality/distance;
- rain particle density/distance;
- lightning visual quality;
- reflection update cadence;
- puddle/reflection quality;
- vegetation wind detail;
- shadow distance;
- weather VFX distance;
- optional module disablement.

UI/settings code later consumes project configuration, never direct Enviro
components.

Do not expose unsupported toggles.

## Save/restore production contract

Integrate with the existing save abstraction only as far as current milestone
architecture allows.

Required restore order:

1. restore project time/weather/wetness/lightning state;
2. validate IDs/config versions;
3. reconstruct schedule/transition;
4. apply spawn/load lightning cooldown;
5. issue one coherent presentation sync;
6. then reveal/load the world without a clear-sky flash.

Do not serialize Enviro references or runtime objects.

Final file hardening remains Milestone 09.

## Audio/UI/vehicle/water outputs

Verify stable outputs are consumable without direct Enviro references:

- Wwise/audio weather intensity and events;
- distance-delayed thunder request;
- UI day/time/weather summary;
- road wetness future hook;
- windshield/wiper future hook;
- visibility/headlight context;
- vehicle cooling future hook;
- vegetation wind;
- water wind/rain future hook;
- NPC future hook.

Do not implement final consumers that belong to later milestones.

## DEV tooling

Extend existing tools for production validation:

- inspect authoritative and presented state;
- force profile/transition/time;
- freeze schedule;
- seed control;
- wetness/puddle override;
- shelter overlay;
- duplicate owner scan;
- scene lifecycle scan;
- Enviro binding validation;
- quality tier switch;
- ambient/gameplay lightning controls;
- canonical production capture;
- weather performance route;
- geometry/transform regression diff;
- vendor file change report;
- shipping-content audit.

## Tests

### Automated

- bootstrap lifecycle;
- no duplicate environment owner;
- no direct Enviro dependency outside integration assembly;
- weather survives additive cell load/unload;
- schedule does not reset on streaming;
- save-state restore ordering;
- no immediate strike after restore;
- shelter/interior exclusion;
- global wetness material contract;
- quality tier mapping;
- missing binding reporting;
- WeatherLab excluded from shipping;
- vendor demo/sample scenes excluded from shipping;
- frozen donor-baseline geometry/transforms not modified by weather rollout;
- production override and gameplay stable IDs/transforms unchanged unless explicitly documented;
- inactive prototype custom visuals remain inactive;
- vendor file changes zero.

### Manual production validation

- clear-day route;
- dawn/dusk;
- night with headlights;
- clear → cloud → rain;
- rain → drying;
- heavy rain;
- thunderstorm;
- morning mist/fog;
- exterior → interior → exterior;
- driving across cell boundaries;
- loading during a transition;
- loading wet ground;
- loading after recent lightning;
- representative donor-baseline cells under neutral clear comparison;
- low/medium/high quality.

Do not claim manual validation that was not performed.

## Performance

Measure in a development build where possible:

- Enviro-disabled diagnostic baseline;
- clear;
- partly cloudy;
- overcast;
- drizzle/rain;
- heavy rain;
- fog/mist;
- storm/lightning;
- dawn/dusk/night;
- dense vegetation;
- driving route;
- interior looking out;
- water/shore point;
- additive cell transitions;
- each supported quality tier.

Record:

- CPU/GPU frame time;
- main/render thread;
- memory/allocations;
- clouds;
- fog;
- precipitation;
- reflections;
- shadows;
- vegetation wind;
- wetness/puddles;
- adapter/domain cost;
- scene-load/cleanup spikes.

Do not invent targets or hide a regression by lowering unrelated world quality.

## Build/content audit

Ensure shipping configuration excludes:

- WeatherLab;
- Enviro demo/sample scenes;
- Azure Sky production objects;
- obsolete HDRP sky/cloud/fog placeholders;
- reference-only world scenes;
- donor assets;
- editor-only diagnostics;
- temporary test materials/prefabs;
- duplicate environment managers.

Do not delete purchased vendor content merely to exclude it from builds.

## Documentation

Create/update:

- `Docs/Weather/PRODUCTION_ROLLOUT.md`;
- `Docs/Weather/PRODUCTION_OWNER_MATRIX.csv`;
- `Docs/Weather/QUALITY_TIERS.md`;
- `Docs/Weather/PRODUCTION_BINDING_VALIDATION.md`;
- `Docs/Weather/PRODUCTION_SAVE_RESTORE.md`;
- `Docs/Weather/PRODUCTION_PERFORMANCE.md`;
- `Docs/Weather/PRODUCTION_REGRESSION.md`;
- `Docs/Weather/WORLD_FIDELITY_WEATHER_REGRESSION.md`;
- `Docs/Weather/SHIPPING_CONTENT_AUDIT.md`;
- `Docs/Weather/KNOWN_LIMITATIONS.md`;
- `Docs/Milestones/MILESTONE_07C_REPORT.md`;
- summary `Docs/Milestones/MILESTONE_07_REPORT.md`.

## Non-goals

Do not implement:

- Wwise content;
- final UI;
- winter/snow/full seasons;
- separate local storm fronts across the compact map;
- full water/swimming/buoyancy;
- final windshield/wipers;
- final wet tire calibration;
- forest fires;
- electrical grid;
- broad NPC weather behavior;
- unrelated world art redesign;
- vendor patches;
- new gameplay locations.

## Definition of done

1. Project-owned time/weather/wetness/lightning state is authoritative in
   production.
2. Enviro follows it through one validated adapter.
3. No duplicate environment owners exist.
4. Streaming does not reset or duplicate weather state.
5. Production material/wetness/shelter hooks work within bounded scope.
6. Fog/water strategy is stable without vendor patching.
7. Ambient and gameplay lightning remain separate and fair.
8. Enviro audio is disabled in production.
9. Quality tiers are measured and coherent.
10. Save/restore contract avoids visible flashes and immediate strikes.
11. Shipping content audit passes.
12. Weather rollout did not damage donor-fidelity or stable transforms of
    production cells.
13. Tests, captures and performance data are honest.
14. Vendor file change count is zero.
15. The project remains playable and ready for Milestone 08.

## Final response

Report:

1. Production owner migration.
2. Bootstrap/streaming lifecycle.
3. Enviro integration and exact local version.
4. Time/weather/wetness/lightning behavior.
5. Fog/water/shelter strategy.
6. Material/wind integration.
7. Save/restore ordering.
8. Cross-system outputs.
9. Quality tiers.
10. Tests and manual validation.
11. Performance.
12. World-fidelity regression results.
13. Shipping-content audit.
14. Files changed.
15. Vendor files changed.
16. Known limitations.
17. Readiness for `08_WWISE_AUDIO.md`.

Stop after Milestone 07C.
