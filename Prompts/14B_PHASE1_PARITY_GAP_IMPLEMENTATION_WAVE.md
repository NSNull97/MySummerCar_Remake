/plan

# MILESTONE 14B — IMPLEMENT ONE APPROVED PHASE 1 GAP WAVE

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.


## Required user input

The user must explicitly provide one approved `WaveId` from
`Docs/Phase1/GapWaves/PHASE1_GAP_WAVE_PLAN.md`.

If no WaveId is provided, stop and ask for it.

## Objective

Implement only the FeatureIds assigned to the approved wave.

## Rules

- re-verify each gap before changing code/content;
- use existing domain architecture and do not create parallel managers;
- update runtime, temporary legacy presentation, audio/UI, save DTOs and tests
  needed by those exact rows;
- do not implement another wave opportunistically;
- update matrix rows with evidence;
- run focused donor comparison and save/load scenarios;
- if a row requires a broader unapproved dependency, mark it blocked and stop
  that row rather than broadening scope.

## Completion

Create `Docs/Milestones/MILESTONE_14B_<WaveId>_REPORT.md`.

Stop after one wave. Repeat this prompt with explicit approval until 14A reports
no required gaps.
