/plan

# MILESTONE 06B2 — DONOR MAP STREAMING CELLIZATION AND ACTIVE WORLD PROFILE

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Milestones/MILESTONE_06B1_REPORT.md`;
- all `Docs/WorldBaseline/*` outputs from 06B1;
- current world streaming architecture, registries, cells, scene lifecycle,
  tests, and performance reports;
- current Git status and diff.

## Entry gate

Stop if:

- 06B1 is not closed;
- no canonical sanitized donor baseline exists;
- forbidden donor runtime components remain;
- the project does not compile;
- the existing streaming architecture has not been inspected;
- a broad unrelated diff is present.

## Objective

Use the existing streaming architecture to make the sanitized donor map —
including its sanitized temporary donor materials and referenced textures — the
active temporary runtime world baseline, distribute eligible world objects into
deterministic additive cells without changing the original map, preserve large
continuous objects safely, and disable the inaccurate custom visual content of
the two prototype cells.

The active baseline must be visually readable and recognizable as the original
map. A grey, magenta, or materially anonymous world is not an acceptable 06B2
result when the corresponding donor textures/material definitions are available.

This milestone is cellization and activation.

It is not world remastering, weather integration, or a streaming rewrite for its
own sake.

## Architecture target

Use the existing project architecture where possible. The logical result must be
equivalent to:

```text
World_Global_Legacy
World_Cell_<ID>_Legacy
World_Cell_<ID>_Gameplay
World_Cell_<ID>_ProductionOverride
```

Meaning:

- `Global_Legacy`: continuous terrain, water, large roads/bridges, and other
  cross-cell objects that cannot yet be split safely;
- `Cell_Legacy`: donor-derived static visuals/collision owned by a cell;
- `Cell_Gameplay`: project-owned anchors, triggers, stable IDs, AI/navigation and
  gameplay data;
- `Cell_ProductionOverride`: future rebuilt art that can replace matching legacy
  objects without moving gameplay anchors.

Do not force literal scene names when the current architecture already provides
an equivalent separation. Document the mapping.

## Non-destructive cellization rules

1. Reuse the existing world-cell grid, registry, load radius, lifecycle and scene
   naming unless evidence proves a bounded fix is required.
2. Do not redesign roads, terrain, buildings, vegetation, or landmarks.
3. Do not destructively split a continuous terrain, road, bridge, water body, or
   large cross-cell mesh.
4. Assign normal static objects deterministically using documented bounds/pivot
   rules.
5. Objects crossing cell boundaries must use one explicit policy:
   - global;
   - owner cell with preload margin;
   - approved deterministic split by a tested tool.
6. Re-running cellization must produce stable object-to-cell assignments.
7. Keep raw donor payloads outside Git; generated local scenes/assets remain
   reproducible from manifests/tools.
8. Do not add donor scripts, lighting, weather, audio, cameras, UI, or gameplay
   logic.
9. Do not attach per-object runtime `Update` scripts merely for streaming.
10. Do not turn every decorative object into a persistent `StableEntityId` unless
    gameplay/save/replacement requirements justify it.

## Temporary donor material and texture activation

Activate the original map's already extracted static material definitions and
referenced textures as temporary baseline presentation.

This is permitted only under the `TemporaryDirectImport` runtime-baseline policy.
It is not final production art and must remain replaceable.

### Source and reuse rules

1. Read the 06B1 material/texture audit and canonical-source manifest first.
2. Reuse the canonical already extracted/imported material and texture data when
   it exists. Do not create a second parallel texture dump or duplicate import.
3. If a referenced static texture or material definition is missing from staging,
   extend the existing safe read-only extraction/import pipeline only for the
   missing referenced assets. Do not modify donor files.
4. Keep raw donor payloads outside Git. Commit only project-owned tools,
   manifests, mappings, reports, metadata, and tests.
5. Store generated local baseline presentation assets under an existing
   equivalent of:

   ```text
   Assets/Game/LegacyImport/RuntimeBaseline/Generated/Materials/
   Assets/Game/LegacyImport/RuntimeBaseline/Generated/Textures/
   Assets/Game/LegacyImport/RuntimeBaseline/Generated/TerrainLayers/
   ```

6. Classify every generated donor-derived presentation asset as
   `TemporaryDirectImport` and preserve source path/hash provenance.
7. Deduplicate by canonical source identity/hash plus conversion settings. Cells
   must reference shared generated assets rather than creating copies per cell or
   per renderer.

### HDRP compatibility conversion

Do not import or compile donor shader code. Build project-owned deterministic
HDRP compatibility materials from donor material data.

Support at least the source categories actually present in the canonical map:

- opaque lit surfaces -> HDRP Lit compatibility material;
- alpha-cutout foliage, fences, signs and tree walls -> HDRP Lit with alpha
  clipping and correct double-sided/culling behavior where required;
- transparent glass or simple transparent surfaces -> bounded HDRP transparent
  compatibility material;
- genuinely unlit backdrops/signs -> HDRP Unlit compatibility material;
- emissive source materials -> HDRP Lit/Unlit emission mapping;
- terrain source layers -> generated HDRP-compatible terrain layers when the
  active terrain implementation requires them.

Preserve when present and meaningful:

- base/albedo texture;
- color tint;
- alpha channel and cutoff threshold;
- UV scale and offset;
- normal map;
- emission texture/color;
- source transparency/culling intent;
- renderer material-slot order.

Use explicit neutral fallback values when the donor source does not provide a
modern PBR channel. Do not invent final roughness, metallic, height, AO, detail,
wetness, dirt, or damage authoring during 06B2.

Do not:

- AI-upscale, repaint, sharpen, or procedurally "improve" donor textures;
- treat generated compatibility materials as `ProductionReady`;
- copy donor lighting, lightmaps, reflection probes, post-processing, skybox,
  fog, weather logic, water logic, or custom runtime shader systems;
- silently replace unavailable textures with arbitrary modern assets;
- instantiate unique runtime materials through `Renderer.material`;
- create one material asset per object when multiple objects share a source
  material.

A simple project-owned temporary water presentation may be used if required to
make the lake visible, but donor water/weather runtime logic must remain excluded.
Final water belongs to the later water/remaster work.

### Texture import policy

For every generated baseline texture:

- preserve source dimensions; never upscale;
- preserve alpha when used;
- set sRGB/linear import state according to semantic use;
- import normal maps as normal maps;
- enable mipmaps for world-space textures unless an inspected use case requires
  otherwise;
- disable Read/Write unless a verified tool/runtime requirement needs it;
- use Windows-x64-appropriate compression without destroying signage, alpha
  cutouts, or low-resolution source readability;
- use texture streaming where supported and appropriate;
- preserve/document wrap mode, filter mode, and anisotropy decisions;
- record every non-default override in the generated manifest.

### Missing and unsupported presentation

Create an explicit diagnostic fallback material for missing/unsupported sources.
It must be visually obvious in development, but it must not be confused with the
standard Unity magenta shader-error state.

Generate a report separating:

- converted successfully;
- source texture missing;
- unsupported source shader/material;
- intentionally excluded lighting/weather/water presentation;
- ambiguous alpha/culling semantics requiring visual review;
- duplicate source collapsed into an existing generated asset.

Do not silently produce a white or grey world and call the baseline complete.

### Runtime and streaming behavior

- Use shared materials and textures across cells.
- Cell unload must not destroy or duplicate globally shared material assets.
- Repeated load/unload must not increase material instance or texture counts.
- Use MaterialPropertyBlock only for bounded per-instance values such as an
  inspected source tint; do not clone materials for this purpose.
- The active-world profile must support at least:
  - `LegacyTextured` — donor baseline textures/material compatibility active;
  - `LegacyDiagnostic` — material/source diagnostics visible;
  - `PrototypeHidden` — inaccurate custom prototype visuals inactive.
- Future `ProductionOverride` assets must be able to disable matching legacy
  renderers without changing gameplay anchors or cell ownership.

### Visual baseline review

Under neutral daytime lighting, inspect at minimum:

- player home/garage;
- Teimo store and fuel area;
- Fleetari area;
- church/town landmark area;
- representative paved and gravel roads;
- representative forest/tree-wall region;
- lake/shoreline visibility;
- both formerly inaccurate prototype-cell regions.

The purpose is not modern beauty. The purpose is to make the imported world
immediately readable and recognizably identical to the original map while later
production replacement work remains pending.

## Baseline object identity

Create a project-owned mapping for legacy visuals, for example:

- `LegacyWorldObjectId` or equivalent provenance ID;
- `ReplacementKey` for future production override;
- source path/hash/reference;
- assigned cell/global ownership;
- renderer/collider activation state;
- classification `TemporaryDirectImport`.

Persistent gameplay entities continue to use `StableEntityId`.

Do not use donor names, scene paths, or Unity instance IDs as save identity.

## Prototype cell migration

For the two custom cells that do not resemble the original locations:

- keep valid cell IDs, registry entries, load/unload code, test fixtures, and
  project-owned gameplay metadata;
- disable the custom visual roots from the active world profile;
- mark them `PrototypeOnly / RejectedForFidelity / Inactive`;
- activate the donor baseline objects that occupy the same world region;
- do not manually remodel either location;
- do not delete the old visual assets until dependency scans and human review
  make deletion safe;
- ensure no duplicate collision/rendering remains active.

The result must look like the actual original map because it is using the donor
baseline, not because of a new interpretation.

## Global versus cell-owned content

Audit and classify at least:

- terrain/world mesh;
- roads and bridges;
- rail line;
- lake/water presentation;
- major backdrop/tree-wall objects;
- buildings;
- props;
- vegetation;
- signs/fences;
- colliders;
- lighting/weather/audio objects that must be excluded;
- source materials, texture dependencies, terrain layers, alpha-cutout and
  transparent presentation categories.

Create:

`Docs/WorldBaseline/GLOBAL_AND_CELL_OWNERSHIP_MATRIX.csv`

Every global exception must explain why it is not cellized yet.

## Gameplay separation

Ensure project-owned gameplay data remains separate from donor visuals.

Requirements:

- gameplay anchors survive when legacy visual objects are disabled;
- interaction logic does not search donor hierarchy names;
- save data does not serialize legacy scene object references;
- production override can replace a legacy renderer/collider through a stable
  replacement key;
- cell loading order does not create duplicate gameplay objects;
- gameplay cells may load even when a visual override is absent.

Add validation for this separation.

## Streaming implementation

Use existing tools and patterns first.

Support:

- initial bootstrap load;
- player-driven cell streaming;
- vehicle-speed preload margin;
- global scene lifetime;
- cell unload and clean reload;
- deterministic cell content manifests;
- editor-only cell visualization;
- one-click legacy/prototype/production visibility modes;
- no duplicate object after repeated load/unload;
- no visible world-origin transform drift.

Do not implement an unrelated streaming framework.

## Collision and traversal

Use sanitized donor collision temporarily when safe.

Fix only technical blockers required for the baseline to be playable:

- missing road/bridge/terrain collision;
- duplicate active colliders;
- invalid collision layers;
- player/vehicle falling through world due to import or streaming errors;
- blocking seams created by cellization;
- incorrect spawn/return positions;
- out-of-bounds recovery.

Do not remodel visible terrain voids, sprite forests, flat fields, or legacy proxy
art in this milestone. Catalogue them as later remaster debt.

Use a project-owned out-of-bounds recovery volume or safety mechanism where
needed. Do not disguise it as a final visual fix.

## Editor tooling

Create or extend bounded tools for:

- previewing cell bounds and ownership;
- dry-run assignment report;
- deterministic cell generation/update;
- global-object exceptions;
- duplicate renderer/collider scan;
- forbidden component scan;
- legacy/prototype/production visibility mode;
- active world profile selection;
- object replacement-key inspection;
- cell load/unload smoke route;
- manifest export.

All generated changes must be repeatable and reviewable.

## Tests

Add or run tests for:

- deterministic object-to-cell assignment;
- stable global/cell ownership;
- no forbidden donor components;
- no gameplay dependency on donor hierarchy paths/names;
- no duplicate renderer/collider in active profile;
- global scene remains loaded while cells change;
- repeated cell load/unload;
- neighboring-cell preload while driving;
- gameplay anchors persist across visual replacement mode;
- prototype custom visuals remain inactive;
- donor baseline visual content is active for the two affected regions;
- donor baseline materials/textures are active for representative regions;
- no unexpected Unity magenta shader-error materials remain;
- material/texture conversion is deterministic and source-hash deduplicated;
- repeated cell load/unload does not increase material instance or texture counts;
- no donor shader/runtime lighting/weather/water system entered the baseline;
- alpha-cutout vegetation/tree-wall/signage presentation is visually reviewed;
- project compiles;
- no raw donor payload committed.

Manual checks must include:

- launch from bootstrap;
- walk/drive through representative cell boundaries;
- visit both formerly inaccurate custom-cell regions and confirm the donor map,
  donor baseline materials and donor baseline textures are shown instead;
- inspect the required neutral-lighting visual review locations and confirm they
  are recognizable rather than grey, white, or magenta;
- inspect alpha cutout, transparency, UV scale/offset and material-slot ordering;
- inspect road, bridge, terrain and collision transitions;
- unload/reload representative cells;
- confirm no duplicate world appears;
- confirm exact original scale and recognizable layout remain intact.

## Performance baseline

Measure at minimum:

- bootstrap/global load time;
- representative cell load/unload time;
- memory after initial load;
- memory after travel across several cells;
- main-thread and render-thread spikes during streaming;
- active renderer/collider counts;
- unique generated material count and runtime material-instance count;
- resident/streamed texture memory for representative cells;
- duplicate texture/material savings from canonical deduplication;
- comparison against the previous prototype world setup when available.

Do not perform broad art optimization here. Record expensive legacy content for
later optimization/remaster work.

## Output

Create:

- `Docs/WorldBaseline/CELLIZATION_RULES.md`;
- `Docs/WorldBaseline/GLOBAL_AND_CELL_OWNERSHIP_MATRIX.csv`;
- `Docs/WorldBaseline/LEGACY_OBJECT_CELL_MANIFEST.csv`;
- `Docs/WorldBaseline/PROTOTYPE_CELL_MIGRATION_REPORT.md`;
- `Docs/WorldBaseline/GAMEPLAY_VISUAL_SEPARATION_REPORT.md`;
- `Docs/WorldBaseline/STREAMING_VALIDATION_REPORT.md`;
- `Docs/WorldBaseline/STREAMING_PERFORMANCE_BASELINE.md`;
- `Docs/WorldBaseline/LEGACY_MATERIAL_TEXTURE_MANIFEST.csv`;
- `Docs/WorldBaseline/MATERIAL_SHADER_MAPPING.md`;
- `Docs/WorldBaseline/BASELINE_VISUAL_COMPLETENESS_REPORT.md`;
- `Docs/WorldBaseline/TEXTURE_MEMORY_BASELINE.md`;
- `Docs/Milestones/MILESTONE_06B2_REPORT.md`.

## Definition of done

1. The sanitized donor map is the active temporary runtime world baseline.
2. Existing streaming architecture is reused and documented.
3. Cell-eligible donor objects are assigned deterministically.
4. Continuous/cross-cell objects remain safe and explicitly global where needed.
5. The two inaccurate custom visual cells are inactive and replaced at runtime by
   the exact donor baseline content.
6. Sanitized temporary donor materials and referenced textures are active through
   project-owned HDRP compatibility materials, making representative areas
   recognizable and free of unexpected magenta/blank presentation.
7. Material/texture generation is deterministic, deduplicated, provenance-tracked
   and does not import donor shader/runtime lighting/weather systems.
8. Gameplay data is independent from donor visual hierarchy.
9. Representative walking/driving cell transitions work.
10. Collision blockers introduced by import/cellization are fixed or clearly
    blocked.
11. Performance, material counts and texture memory are measured honestly.
12. The report gives a go/no-go for full baseline validation in 06B3.

## Final response

Report:

1. Existing streaming architecture reused.
2. Global and cell-owned content policy.
3. Generated/updated cells and manifests.
4. Prototype-cell migration.
5. Temporary donor material/texture activation and HDRP mapping.
6. Missing/unsupported presentation and visual completeness.
7. Gameplay/visual separation.
8. Collision/traversal results.
9. Tests and manual checks actually run.
10. Performance, material counts and texture memory.
11. Files changed.
12. Remaining blockers/risks.
13. Go/no-go for `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`.

Stop after 06B2. Do not start weather.
