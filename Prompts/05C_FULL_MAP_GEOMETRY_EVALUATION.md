/plan

# MILESTONE 05C — FULL MAP GEOMETRY EVALUATION BASELINE

Read `AGENTS.md` completely.

Read:

- `Prompts/04A_FULL_WORLD_GEOMETRY_TRANSFER.md`;
- `Docs/Milestones/MILESTONE_04A1_REPORT.md`;
- `Docs/WorldTransfer/WORLD_FIDELITY_REPORT.md`;
- `Docs/WorldTransfer/WORLD_TRANSFER_VALIDATION.md`;
- `Docs/Milestones/MILESTONE_05A_REPORT.md`;
- all existing `MILESTONE_05A_BATCH_*` reports;
- current Git status and diff.

## Objective

Create a complete, removable, reference-only visualization of the discovered
donor map geometry so the whole map can be inspected for:

- mesh appearance and topology;
- world position, rotation and scale;
- terrain, road, shoreline and water alignment;
- building exterior/interior alignment;
- pivots and object relationships;
- missing geometry and bounds-only fallbacks;
- cell ownership and seams;
- obvious spatial outliers.

This milestone evaluates the map only. It does not recreate gameplay systems.

## Visual identity and void audit requirement

In addition to geometry, mark internal terrain voids, sprite/tree walls, missing
ground, clipped fields, fake backdrops and any location whose production
replacement would be impossible to validate from current reference data.

Create canonical real-donor camera fixtures for high-priority cells. Do not use
AI-generated concepts as fidelity evidence.

## Scope

Include:

- all `ReferenceWorldEligible` records from the frozen 04A1 database;
- donor meshes copied only into ignored `LegacyImport/ReferenceOnly` storage;
- project-owned neutral/category debug materials, without donor textures;
- exact serialized world position, rotation and scale;
- bounds proxies for unresolved or non-mesh records;
- 49 partition cells plus global/persistent/bootstrap content;
- a full-map Editor overview and free-fly inspection route;
- deterministic coverage, transform and missing-geometry validation;
- representative captures and a defect register.

Exclude:

- production use of donor meshes or textures;
- final HDRP art, materials, vegetation or lighting;
- gameplay scripts, NPCs, quests, economy, survival and traffic;
- vehicle simulation;
- weather, audio, UI and save implementation;
- 05B/06 or later system work;
- public/distributable builds containing donor content.

## Donor-content boundary

The donor installation remains read-only. Reuse only the already frozen
external AssetRipper extraction. Imported donor meshes must live under:

`Assets/Game/LegacyImport/ReferenceOnly/World/`

This tree must remain ignored, tagged `EditorOnly`, excluded from Build
Settings and removable without breaking production content. Do not import
donor textures, materials, scripts, assemblies or executable components.

## Required implementation

1. Freeze and report the 04A1 database/source revision.
2. Synchronize only the unique meshes used by reference-world records from
   external staging into the ignored reference mesh library.
3. Preserve source mesh GUID provenance and verify every copied asset.
4. Generate actual-mesh instances using converted positions plus audited
   source rotation/scale.
5. Generate explicit bounds proxies where a mesh is absent or unresolved.
6. Separate actual geometry and fallback proxies in the scene hierarchy.
7. Generate all cell/global/persistent/bootstrap scenes deterministically.
8. Provide an Editor command that opens the entire map additively for review.
9. Keep reference scenes and mesh assets out of normal builds.
10. Validate counts, stable IDs, transforms, mesh resolution, cell coverage,
    provenance and production-dependency isolation.
11. Produce an inspection checklist and defect register.

## Acceptance criteria

- `3 842` reference-world records are represented exactly once.
- `49` concrete cells plus the global layer are generated.
- Every record uses either an actual synchronized mesh or an explicit bounds
  fallback; nothing is silently dropped.
- Actual mesh instances preserve finite position, normalized rotation and
  non-zero scale from the frozen fixture.
- Reference-only donor content has zero production/build dependencies.
- The complete map can be opened through one documented Editor command.
- Terrain/road/water/building coverage and known limitations are reported
  honestly.
- Automated validation passes, or exact blockers are recorded.
- Manual visual approval is not claimed without user review.

## Outputs

Create or update:

- `Docs/WorldTransfer/M05C_GEOMETRY_EVALUATION_COVERAGE.md`;
- `Docs/WorldTransfer/M05C_GEOMETRY_DEFECTS.csv`;
- `Docs/WorldTransfer/M05C_INSPECTION_CHECKLIST.md`;
- `Docs/Milestones/MILESTONE_05C_REPORT.md`;
- `Prompts/README.md`;
- relevant world-transfer tools and tests.

## Stop condition

Stop after the complete map geometry-evaluation baseline and its validation.
Do not implement gameplay systems or final graphics in this milestone.
