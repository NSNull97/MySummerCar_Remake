/plan

# MILESTONE 10B — FULL NPC ROSTER, DIALOGUE, AND SCHEDULE PARITY

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read the entire locked `NPC_ROSTER.csv`, 10A architecture/report and all matrix
rows owned by 10B.

## Objective

Populate and verify every donor-evidenced NPC required by Phase 1.

The prompt must not rely on a hand-written shortlist. The roster created in 08B
is authoritative.

## For each NPC

Implement and record, where donor evidence requires it:

- stable ID and definition;
- legacy presentation prefab;
- spawn/home/work anchors;
- schedule and off-screen progression;
- movement/vehicle association;
- dialogue lines, conditions and cooldowns;
- relationship/flag state;
- event/job/service hooks;
- reactions and special states;
- animation/audio mapping;
- save/load behavior;
- tests and donor comparison evidence.

Background/crowd NPCs may share lightweight systems, but named/critical NPCs
must not be collapsed into generic placeholders.

## Batch rule

If the roster is too large for one safe implementation session, use the batches
approved in `PHASE1_EXECUTION_PLAN.md`. Complete one named batch at a time and
run closeout before the next. Never declare 10B complete while required roster
rows remain unverified.

## Definition of done

Every required NPC row is `Verified`, `KnownDifferenceApproved` or honestly
blocked with evidence; no required NPC is invisible, silent when feedback is
required, permanently stuck, non-persistent or dependent on donor runtime code.

Stop after 10B roster closure.
