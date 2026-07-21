/plan

# MILESTONE 11B — TRAFFIC, PUBLIC TRANSPORT, TRAIN, AND ROUTE AI PARITY

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read route/road graph data, NPC/vehicle systems, world streaming and all matrix
rows assigned to 11B.

## Objective

Reproduce donor-evidenced moving-world transportation behavior without donor
runtime code.

Implement as required:

- traffic spawn/despawn and route schedules;
- driver/vehicle associations;
- donor-faithful speed, stops, encounters and recovery;
- bus/public transport flow;
- train movement, crossings and hazards;
- scripted/route-based special vehicles;
- off-screen schedule simulation;
- collision/event hooks;
- player interaction/ride behavior;
- save/load and streamed-cell continuity;
- audio and temporary legacy presentation;
- deterministic DEV scenarios and route traces.

Do not upgrade the system into a new sophisticated traffic simulation during
Phase 1. Stability fixes are allowed; behavior enhancements go to Phase 2.

Update parity/save matrices and stop after 11B.
