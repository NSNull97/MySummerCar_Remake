/plan

# MILESTONE 14A — PHASE 1 PARITY GAP AUDIT AND IMPLEMENTATION-WAVE PLAN

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read all milestone reports through 13C, complete rosters, save coverage, build
validation and latest review.

## Objective

Perform a read-only full Phase 1 gap audit. Do not fix gaps in this milestone.

## Audit

For every required matrix row verify independently:

- runtime implementation exists;
- actual player-visible behavior works;
- required NPC/vehicle/item/presentation exists;
- save/load coverage exists;
- UI/audio hooks are real, not demo data;
- tests/captures/play scenarios exist;
- no donor runtime code is required;
- known differences have user approval.

Also scan for donor features omitted from the matrix and add evidence-backed rows.

## Wave plan

Create `Docs/Phase1/GapWaves/PHASE1_GAP_WAVE_PLAN.md` and one manifest per wave.

Each wave must be bounded, dependency-safe and contain explicit FeatureIds.
Do not group unrelated systems merely to reduce the number of waves.

Suggested wave severity order:

1. crashes/data loss/runtime independence;
2. missing progression/NPC/vehicle core;
3. missing jobs/services/mechanics;
4. save/load gaps;
5. missing legacy presentation/audio;
6. minor verified parity differences.

## Definition of done

- matrix reflects observed reality, not milestone claims;
- every remaining gap belongs to a named wave or explicit user-decision state;
- no implementation changes were made;
- report gives a go/no-go for starting wave implementation.

Stop after 14A.
