/plan

# MILESTONE 15B — FULL-GAME OPTIMIZATION, CONTENT AUDIT, AND PRIVATE BUILD

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read Phase 1 DoD, full playthrough report, performance budgets, legacy baseline
policies, Enviro/audio/UI/save reports and current build configuration.

## Objective

Measure, optimize and produce a private Windows x64 **full-game Legacy Feature
Complete** development build.

This replaces the old vertical-slice optimization prompt.

## Representative profiling

Profile at minimum:

- fresh and late-game saves;
- full map traversal and cell churn;
- Satsuma with physical clutter;
- populated NPC/service locations;
- traffic/public transport;
- rain/storm and night lighting;
- jobs/story events;
- save/load spikes;
- UI/menu/settings;
- audio voice/streaming load.

Optimize only measured bottlenecks. Do not remove required content to hit FPS.

## Build audit

Allow `TemporaryDirectImport` only in the explicit private Phase 1 profile.
Block donor scripts, assemblies, raw extraction, reference-only content, editor
code, vendor demos, fake UI data and local absolute paths.

Produce:

- full build manifest;
- legacy-content manifest;
- performance report;
- content/assembly/provenance audit;
- private build instructions;
- known limitations clearly separated from missing parity.

Do not call this a production/remastered release candidate.

Stop after build validation.
