/plan

# MILESTONE 15C — PHASE 1 STABILIZATION, REGRESSION, AND RC GATE

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read Phase 1 Definition of Done, build/playthrough reports, parity matrix, known
issues, save/recovery and latest technical review.

## Objective

Turn the complete private Legacy Feature Complete build into a stable Phase 1
release candidate for user testing.

No new features or production-art work are allowed.

## Priority

1. crashes and data loss;
2. save corruption;
3. progression blockers/softlocks;
4. missing required feature behavior;
5. vehicle/NPC/world instability;
6. input/UI blockers;
7. severe performance/audio issues;
8. legacy import defects that prevent readability/use;
9. minor non-production polish.

## Required regression suites

- complete parity matrix validation;
- full-game smoke/playthrough paths;
- save fixtures and migration/recovery;
- all roster spawn/availability checks;
- all jobs/events/services availability;
- full map traversal and OOB recovery;
- late-game performance soak;
- repeated new/load/quit cycles;
- no donor runtime dependency;
- build content/provenance audit.

## Phase 1 gate

Create `Docs/Phase1/PHASE1_GATE_REPORT.md` with explicit pass/fail for every DoD
section.

Only the user can set `Phase1Approved=true`.

If approved, recommend exactly one next milestone:

`16_PHASE2_PRODUCTION_REMASTER_AND_POLISH_PLANNING.md`

Stop after the gate.
