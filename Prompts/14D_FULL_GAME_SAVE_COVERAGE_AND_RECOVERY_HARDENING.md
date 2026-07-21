/plan

# MILESTONE 14D — FULL-GAME SAVE COVERAGE AND RECOVERY HARDENING

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read every domain save addition, parity matrix, full-game save coverage, stable
IDs, world streaming and event graphs.

## Objective

Close save/load coverage for the complete Phase 1 game and harden recovery.

## Required work

- audit every required FeatureId for mutable state;
- add missing DTOs, participants, restore ordering and migrations;
- verify NPC schedules/dialogues/relationships;
- verify all vehicles, keys, fuel, damage and location;
- verify items/containers/consumables;
- verify jobs, services, transactions and bills;
- verify story/events/rally/police/jail/hospital/death state;
- verify media/minigames and phone/mail queues;
- verify unloaded-cell deferred restore;
- verify temporary presentation replacement keys do not leak into save identity;
- implement backup, corruption detection, unresolved-ID reporting and safe
  recovery paths;
- measure representative late-game save size and timings.

Create clean, mid-game, late-game, clutter-heavy and failure fixtures.

No required feature may remain `SaveCoverage=None` at completion.

Stop after 14D.
