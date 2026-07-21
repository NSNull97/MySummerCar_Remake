/plan

# MILESTONE 13A — STORY, RELATIONSHIPS, PROGRESSION, AND EVENT-GRAPH PARITY

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read the locked story/event roster, NPC/job/service systems and donor save/event
evidence.

## Objective

Reproduce every donor-evidenced progression chain, relationship state and major
conditional event assigned to 13A using project-owned event logic.

## Architecture

Create or align:

- stable event/progression IDs;
- explicit prerequisites and outcomes;
- event state machine/graph independent from scenes;
- delayed/time/calendar conditions;
- NPC, phone, item, vehicle, economy and world hooks;
- mutually exclusive and repeatable rules;
- relationship/flag state only as required by donor behavior;
- failure/recovery and safe load behavior;
- trace/debug graph;
- save DTOs and migration coverage.

Do not compile donor PlayMaker FSMs or translate them mechanically into one giant
manager. First specify visible donor behavior and dependencies, then implement
clean project-owned logic.

Verify each chain from clean prerequisites through outcome and save/load at
intermediate states.

Stop after all 13A rows are verified.
