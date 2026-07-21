/plan

# MILESTONE 09A — FULL-GAME NATIVE SAVE FOUNDATION

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read all existing save/stable-ID/world-streaming/vehicle/weather/UI documents.

## Objective

Replace the vertical-slice save assumption with an extensible native save
foundation capable of owning the complete Phase 1 game.

Implement the framework and current implemented domains now. Do not fabricate
state for features that do not exist yet. Every later milestone must register
its own DTOs, participants, tests and migration notes.

## Required architecture

Create or align:

- versioned `SaveDocument`, header and metadata;
- slot/storage/atomic-write/backup/recovery services;
- participant registry by project-owned domain IDs;
- stable-entity deferred application for unloaded cells;
- explicit domain DTO boundaries;
- migration pipeline;
- load phases and dependency ordering;
- unresolved-content report;
- save busy/failure events for the approved UI;
- deterministic test fixtures.

Never serialize donor hierarchy, asset paths, vendor runtime objects, Unity
instance IDs or raw scene graphs.

## Full-game coverage contract

Create `Docs/Save/FULL_GAME_SAVE_COVERAGE.csv` with one row per Phase 1 feature or
state domain. Later milestones must update it in the same commit as their runtime
implementation.

Define a mandatory contract/checklist for new Phase 1 domains:

- stable state identity;
- DTO schema;
- collection and restore order;
- unloaded-cell behavior;
- missing-content behavior;
- migration impact;
- round-trip test;
- corruption/recovery behavior.

## Existing-domain implementation

Bring all currently implemented domains through 08A into the new save pipeline,
including world baseline state, player, existing items, Satsuma assembly and
simulation, weather/time/wetness/lightning, settings separation and UI metadata
hooks as actually present.

Do not claim unsupported future domains covered.

## Tools and tests

Add save inspector, slot validator, unresolved-ID view, domain-coverage validator,
round-trip fixtures and interrupted-write/corruption tests.

## Definition of done

- native save foundation compiles and round-trips current domains;
- later gameplay modules have a documented registration path;
- coverage matrix exists and is enforced by validation;
- donor visual baseline identity is not persisted as gameplay authority;
- save/load UI uses real status, not fake data;
- milestone report lists uncovered future domains explicitly.

Stop after 09A.
