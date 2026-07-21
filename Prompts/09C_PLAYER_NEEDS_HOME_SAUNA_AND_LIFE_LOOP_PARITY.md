/plan

# MILESTONE 09C — PLAYER NEEDS, HOME, SAUNA, AND LIFE-LOOP PARITY

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read all 09C-assigned matrix rows, player/interaction/viewmodel architecture,
weather outputs, audio hooks, item system and save coverage.

## Objective

Implement the complete donor-evidenced player survival and domestic life loop
assigned to 09C.

Potential categories to audit from the matrix include needs, sleeping, eating,
drinking, smoking, alcohol, stress, dirtiness, urine, sauna, bathing, home
appliances and death/permadeath rules. Implement only locked donor evidence, but
do not omit a matrix row because it is not listed in this paragraph.

## Architecture rules

- central project-owned needs/metabolism state;
- presentation effects mixed through one bounded perception layer;
- lightweight first-person viewmodel animations only where approved;
- gameplay state never depends on animation frames;
- home/appliance interactions use explicit capabilities and stable IDs;
- weather/audio/UI consume project-owned outputs;
- every persistent state has DTOs and round-trip tests.

## Fidelity

Match donor rates, thresholds, consequences and recovery using captured evidence
and tunable data. Do not modernize away difficulty or add hidden assists.

Fix crashes/softlocks, but document deliberate behavior differences.

## Legacy presentation

Bind donor-compatible temporary models, textures, clips and sounds through
project-owned presenters. No full-body player, generic physical hands or donor
controllers.

Update parity/save matrices and provide a repeatable life-loop test sequence.

Stop after 09C.
