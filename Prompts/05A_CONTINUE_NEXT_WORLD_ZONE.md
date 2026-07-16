/plan

# MILESTONE 05A — CONTINUE THE NEXT WORLD-REMASTER ZONE

Read `AGENTS.md` completely.

Read `Prompts/CURRENT_STATE.md`, `Prompts/PROJECT_DESIGN_GUARDRAILS.md`, and `Prompts/WORLD_CELL_FIDELITY_CAPTURE_GUIDE_RU.md`.

Read:

- `Prompts/05A_WORLD_REMASTER.md`;
- `Docs/Milestones/MILESTONE_05A_REPORT.md`;
- all existing `MILESTONE_05A_BATCH_*` reports;
- `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`;
- `Docs/WorldRemaster/WORLD_REPLACEMENT_LEDGER.csv`;
- `Docs/WorldRemaster/WORLD_ART_BACKLOG.csv`;
- current Git status and diff;
- latest world-remaster validation reports;
- relevant world-transfer source and fidelity reports.

## Objective

Complete one bounded, dependency-safe world-remaster batch.

Do not attempt the entire remaining map.

Do not choose several unrelated zones.

Do not restart the pilot architecture unless a measured pipeline defect blocks
the selected zone.

## Zone selection

Choose the next zone using this priority:

1. A zone explicitly named by the user.
2. A zone marked as the next dependency in `ZONE_REMASTER_STATUS.csv`.
3. A zone required for the current vertical slice.
4. The next zone in the approved roadmap order.
5. The highest-priority unblocked zone.

If selection remains ambiguous, stop and report the candidates instead of
guessing.

Record the selected zone before editing.

## Pre-flight

For the selected zone:

- verify donor/reference geometry;
- verify stable IDs;
- verify cell membership;
- verify terrain and road dependencies;
- verify building/interior dependencies;
- identify required manual-art tasks;
- identify reusable production assets;
- identify existing replacements;
- establish exact acceptance criteria;
- estimate scope and stop if it is too large for one bounded batch.

If the zone is too large, split it into a documented sub-zone and process only
that sub-zone.

## Donor-identity precondition

Before authoring the selected zone, freeze a real-donor visual fixture set.
AI-generated concepts are not valid layout/identity references.

The fixture must include matched canonical cameras, neutral clear daylight,
recorded FOV/eye height, road approach, landmark/façade views, silhouette, and a
vehicle-seat approach.

If the fixture is missing, stop and request the exact captures. Do not generate a
generic Finnish substitute.

The target is reconstruction, not reinterpretation. A prettier but unrecognizable
zone fails the batch.

## Required batch workflow

1. Freeze the reference fixture version.
2. Verify transforms, bounds, pivots, and landmark anchors.
3. Reuse approved production systems and material families.
4. Create or improve production replacements required by the zone.
5. Keep manual hero-art tasks honest and explicit.
6. Integrate terrain, roads, buildings, interiors, props, vegetation, water,
   infrastructure, collision, and LOD only where applicable.
7. Build the production cell or cells.
8. Run parity validation.
9. Run streaming validation.
10. Run player and vehicle-clearance checks where relevant.
11. Run material/texture/LOD/collision audits.
12. Capture representative comparison views.
13. Measure performance for the selected zone.
14. Update all ledgers and status files.
15. Stop after this batch.

## Constraints

- Preserve stable world IDs.
- Preserve gameplay-critical dimensions.
- Preserve road routes and landmark positions.
- Do not use donor textures as final production textures.
- Do not make production prefabs depend on donor meshes.
- Do not silently move interaction anchors.
- Do not edit generated scenes manually without an explicit override record.
- Do not introduce a second world-placement source of truth.
- Do not add unrelated gameplay.
- Do not install asset packs or packages silently.
- Do not call blockouts final art.
- Do not mark the zone complete while known blockers remain unreported.

## Visual identity acceptance

Before reporting the batch as ready:

- capture donor/reference, production-before and production-after views;
- use matched camera/FOV/time and neutral presentation;
- document road, terrain, building, landmark, clutter and vegetation differences;
- run a stable transform/geometry diff;
- state whether the place is recognizable without labels;
- provide a human approval package.

Codex may set only `ProductionCandidate / AwaitingHumanApproval`. It may not
self-assign `Approved` or `Complete`.

## Batch output

Create:

`Docs/Milestones/MILESTONE_05A_BATCH_<NN>_<ZONE_ID>.md`

The report must include:

- selected zone/sub-zone;
- reason for selection;
- source/reference revision;
- replacements completed;
- manual-art tasks added;
- terrain status;
- road status;
- building/interior status;
- vegetation/prop status;
- water/infrastructure status;
- collision and LOD status;
- parity measurements;
- performance measurements;
- tests;
- blockers;
- exact next zone recommendation.

Update:

- `ZONE_REMASTER_STATUS.csv`;
- `WORLD_REPLACEMENT_LEDGER.csv`;
- `WORLD_ART_BACKLOG.csv`;
- relevant category reports;
- visual regression records;
- `MILESTONE_05A_REPORT.md` summary/coverage section.

## Completion gate

A zone may be marked `Complete` only when its defined acceptance criteria pass.

Otherwise use:

- `NeedsReview`;
- `Blocked`;
- `ProductionCandidate`;
- `Approved`;
- or another status defined by the main 05A prompt.

Stop after one zone or bounded sub-zone.
