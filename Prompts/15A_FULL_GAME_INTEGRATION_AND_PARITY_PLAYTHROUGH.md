/plan

# MILESTONE 15A — FULL-GAME INTEGRATION AND PARITY PLAYTHROUGH

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read all reports through 14D, closed matrix rows, UI reference acceptance,
build configuration and known issues.

## Objective

Integrate and verify the complete Phase 1 game from Main Menu through all major
donor progression paths and continued free play.

This milestone fixes integration defects only. It does not add missing planned
features; such gaps return to 14A/14B.

## Required playthrough matrix

Create `Docs/Phase1/FULL_GAME_PLAYTHROUGH_MATRIX.md` covering:

- fresh New Game bootstrap;
- player life/home loop;
- Satsuma assembly/start/drive/inspection/rally path;
- every vehicle access/use pattern;
- every critical NPC/service/job/story chain;
- police/jail/hospital/death paths;
- phone/mail/economy/media flows;
- save/load at representative boundaries;
- streamed-cell traversal across the full map;
- post-progression free play.

Use DEV scenarios to accelerate setup, but also perform at least one normal flow
per critical path without bypassing the behavior being tested.

## UI/data audit

Remove or disable fake/demo runtime values left from 08A. Approved layouts stay
locked, but panels must bind to real implemented systems or truthfully display
unavailable state.

## Gate

If a required matrix row is missing or unverified, stop and return to gap waves.
Do not hide it as a known issue.

Stop after full integration report and human playthrough checklist.
