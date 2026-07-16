/plan

# MILESTONE 06B1 — EXISTING DONOR MAP BASELINE AUDIT AND SANITIZATION

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- all world extraction/import/transfer reports;
- current world streaming architecture, cell registry, scene manifests, tests,
  and reports;
- donor audit, porting matrix, system map, porting ledger, extraction logs, and
  source hashes;
- reports and Git history for the two custom world cells that were judged not to
  resemble the original locations;
- current Git status and diff.

## User-confirmed current state — authoritative

The following is already done:

- the original game map has been extracted;
- the extracted map has been visually inspected;
- world streaming has already been connected or prototyped;
- two custom production cells exist, but their visual content does not resemble
  the corresponding original locations.

Do not repeat extraction merely because this prompt mentions donor data.

Do not manually redesign or artistically repair the two cells during this
milestone.

## Objective

Identify and lock the canonical already-extracted donor map source, audit the
existing Unity import and streaming implementation, create or align a sanitized
exact-map runtime baseline, and prove that the baseline contains only permitted
world presentation/collision data.

This milestone establishes the canonical source and sanitation boundary.

It does not yet redistribute the entire map into final streaming cells, implement
weather, or remaster world art.

## Non-negotiable strategy

```text
already extracted donor map
        ↓
canonical source + hashes
        ↓
sanitized temporary runtime baseline
        ↓
existing project streaming architecture
        ↓
later production override replacement
```

The baseline is classified as `TemporaryDirectImport`.

It may be used in private local feature-parity builds. It is not final production
art and is not allowed in a distributable/public build without explicit rights.

## Stop conditions

Stop and report instead of guessing when:

- no canonical extracted map source can be uniquely identified;
- multiple incompatible extracted versions exist and their relationship cannot
  be proven;
- coordinate scale/origin is unknown and cannot be established from current
  reports/data;
- the only available map still requires modifying the donor installation;
- the map is encrypted/access-controlled;
- sanitization would require executing donor code;
- a broad streaming rewrite appears necessary before the current implementation
  has been inspected;
- the repository contains an unrelated broad unreviewed diff;
- the project does not compile before the milestone.

Do not use a stop condition to abandon safe audit/report work.

## Phase 1 — current-state inventory

Inspect and document:

- exact canonical extracted-map path(s);
- source files, scene files, asset bundles/exports, manifests, hashes, and tool
  versions;
- whether the full donor map is already imported into Unity;
- existing donor-map scene roots and generated asset paths;
- current scale, origin, axes, world bounds, and coordinate transform;
- terrain/world mesh structure;
- road and bridge structure;
- water and shoreline presentation objects;
- vegetation, tree walls, flat proxy objects, buildings, props, and signs;
- material/texture conversion state;
- collider structure;
- existing scripts/components attached to imported objects;
- current streaming cells, global scenes, registries, loading rules, and test
  coverage;
- the two custom cells and which parts are reusable infrastructure versus
  rejected visual content;
- current gameplay anchors/stable IDs that already exist inside or around those
  cells.

Create:

- `Docs/WorldBaseline/EXISTING_EXTRACTION_AND_IMPORT_AUDIT.md`;
- `Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md`;
- `Docs/WorldBaseline/CURRENT_STREAMING_ARCHITECTURE.md`;
- `Docs/WorldBaseline/PROTOTYPE_CELL_DISPOSITION.md`.

Do not create a second full-map import when one already exists and is usable.

## Phase 2 — canonical source lock

Create a machine-readable source manifest containing at least:

- source revision ID;
- relative source paths;
- file hashes;
- extraction/import tool version;
- map bounds;
- coordinate system;
- scale;
- root transform;
- known global objects;
- known missing/unsupported assets;
- known legacy visual defects;
- provenance/classification.

Suggested output:

`Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json`

or the existing equivalent project-owned manifest format.

Do not copy raw donor payloads into Git to create the manifest.

## Phase 3 — sanitation whitelist

Create or align a deterministic sanitation pipeline for the already extracted
map.

Allowed imported runtime data includes only what is required for the temporary
world baseline, for example:

- transforms;
- mesh/terrain render data;
- static materials/textures as temporary visual presentation;
- LOD data when safe;
- static colliders and simple physical materials when safe;
- renderer bounds and source metadata;
- project-owned baseline/replacement metadata.

Forbidden imported runtime data includes:

- donor `MonoBehaviour` scripts;
- PlayMaker FSMs;
- donor runtime assemblies;
- old `UnityEngine` references;
- player, vehicle, NPC, save, weather, lighting, audio, UI, camera, platform,
  Steam, DRM, or gameplay manager logic;
- runtime object-name lookup dependencies;
- donor scene lifecycle/bootstrap logic.

Use an explicit whitelist, not a blacklist-only approach.

Unknown component types must be reported and excluded until reviewed.

Create:

- `Docs/WorldBaseline/SANITATION_POLICY.md`;
- `Docs/WorldBaseline/SANITATION_COMPONENT_AUDIT.csv`;
- `Docs/WorldBaseline/UNKNOWN_COMPONENTS.md`;
- automated validation that fails when forbidden components appear in the
  runtime baseline.

## Phase 4 — canonical sanitized baseline scene

Create or align one deterministic full-map baseline scene or equivalent test
representation from the existing extracted assets.

Suggested purpose/name:

```text
World_DonorBaseline_Canonical
```

Requirements:

- preserve exact donor transforms and scale;
- use neutral clear/dry development lighting only;
- do not import donor lighting, sky, fog, weather, cameras, or audio;
- do not create production art replacements;
- do not fill terrain voids or replace sprite forests yet;
- do not physically split a single terrain/large road/water object;
- add project-owned metadata needed for later deterministic cellization;
- keep donor-generated runtime payload under the dedicated
  `LegacyImport/RuntimeBaseline` boundary;
- do not make gameplay systems depend on the imported hierarchy;
- do not silently duplicate existing imported objects.

If the canonical map is already imported and sanitized, adopt and validate it
instead of regenerating it.

## Phase 5 — prototype-cell disposition

For the two custom cells that do not resemble the donor locations:

- preserve streaming code, registry entries, cell IDs, test infrastructure, and
  project-owned gameplay metadata when useful;
- classify the custom visual content as
  `PrototypeOnly / RejectedForFidelity / Inactive`;
- do not delete it unless dependency validation proves deletion safe;
- prepare an explicit activation switch so the next milestone can use donor
  baseline visuals in those cell bounds;
- do not hand-remodel the cells now.

Create a disposition table with every retained, disabled, archived, or rejected
asset/scene and the reason.

## Validation

Run or add checks for:

- project compilation;
- canonical source hash stability;
- no donor script/FSM/runtime assembly in the baseline;
- no direct gameplay assembly dependency on donor hierarchy names/paths;
- exact map root transform and scale;
- duplicate imported object detection;
- missing mesh/material/collider report;
- deterministic repeated sanitation/import output;
- canonical baseline scene boot;
- vendor/donor source files unchanged;
- Git contains no raw donor payloads.

Manual checks must include:

- open the canonical baseline;
- confirm the map is the recognizable original donor map;
- confirm the two relevant locations exist in donor form;
- confirm no custom production reinterpretation is active in the canonical
  baseline view;
- inspect representative roads, buildings, terrain, and map bounds.

Do not claim manual checks that were not performed.

## Output

Create:

- `Docs/WorldBaseline/EXISTING_EXTRACTION_AND_IMPORT_AUDIT.md`;
- `Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md`;
- `Docs/WorldBaseline/CURRENT_STREAMING_ARCHITECTURE.md`;
- `Docs/WorldBaseline/PROTOTYPE_CELL_DISPOSITION.md`;
- `Docs/WorldBaseline/SANITATION_POLICY.md`;
- `Docs/WorldBaseline/SANITATION_COMPONENT_AUDIT.csv`;
- `Docs/WorldBaseline/UNKNOWN_COMPONENTS.md`;
- the project-owned baseline source manifest;
- `Docs/Milestones/MILESTONE_06B1_REPORT.md`.

## Definition of done

1. The already extracted donor map source is uniquely identified and hashed.
2. Existing import and streaming work is documented instead of duplicated.
3. A canonical sanitized full-map baseline can be opened in Unity.
4. Exact donor layout, transforms, and scale are preserved.
5. No forbidden donor runtime logic exists in the baseline.
6. The two inaccurate custom cells have a documented inactive/prototype
   disposition without losing useful streaming infrastructure.
7. The project compiles and validation results are honest.
8. The report gives a go/no-go for deterministic cellization in 06B2.

## Final response

Report:

1. Canonical source selected.
2. Existing extraction/import state.
3. Existing streaming state.
4. Sanitization rules and findings.
5. Baseline scene/result.
6. Prototype-cell disposition.
7. Tests and manual checks actually run.
8. Files changed.
9. Remaining blockers/risks.
10. Go/no-go for `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md`.

Stop after 06B1. Do not start cellization or weather.
