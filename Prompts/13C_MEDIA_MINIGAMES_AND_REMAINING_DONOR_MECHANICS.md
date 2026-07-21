/plan

# MILESTONE 13C — MEDIA, MINIGAMES, AND REMAINING DONOR MECHANICS

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read every matrix row assigned to 13C and the corresponding media/audio/UI/item,
world, NPC and save documentation.

## Objective

Implement all remaining donor-evidenced systems not owned by previous domain
milestones. Typical discovery categories include radio, television, computer,
music import, gambling, games, dances/local events and miscellaneous interactive
world systems. The matrix, not this example list, is authoritative.

## Rules

- each implemented feature requires project-owned stable IDs/state;
- use `IAudioBackend` and approved UI contracts;
- use temporary donor presentation only through wrappers and provenance;
- preserve donor scheduling/availability/conditions;
- add save DTOs and tests;
- do not treat a silent placeholder button as feature parity;
- do not create new minigames or content absent from the locked donor version.

At completion, no required matrix row may remain unowned.

Stop after 13C.
