/plan

# MILESTONE 09B — WORLD ITEMS, CONSUMABLES, AND CONTAINERS PARITY

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read the item/consumable rows assigned to 09B, interaction architecture, world
streaming, physical persistence and save coverage documents.

## Objective

Implement every donor-evidenced Phase 1 item/consumable/container behavior
assigned to 09B using project-owned runtime definitions and state.

## Required systems

As required by the matrix, create or align:

- immutable item definitions and mutable instances;
- project-owned stable item IDs;
- pickup/carry/place/rotate/drop/throw integration;
- opened/closed/full/partial/empty state;
- liquids and portioned consumables;
- packaging and empty-container state;
- bounded crates, bags, boxes and other donor containers;
- tool identity and compatibility;
- streaming-cell archival/sleeping behavior;
- out-of-bounds recovery for critical items;
- save DTOs and tests;
- UI interaction/status hooks without RPG inventory conversion.

## Legacy presentation

Use sanitized donor meshes/textures/materials/audio as
`TemporaryDirectImport` where available, through project-owned presenter
prefabs and event IDs. No donor scripts or filename-driven gameplay.

## Scope guardrails

Do not add Phase 2 systems such as advanced garbage clustering, dynamic material
wear, breakable glass simulation, procedural packaging deformation or a
bottomless inventory unless explicitly required by the locked donor version.

## Verification

For every assigned matrix row, capture:

- donor behavior evidence;
- new-runtime use flow;
- save/load round trip;
- streamed-away/returned behavior;
- presentation status;
- known difference.

Update the parity and save-coverage matrices.

Stop after all 09B rows are verified or honestly blocked.
