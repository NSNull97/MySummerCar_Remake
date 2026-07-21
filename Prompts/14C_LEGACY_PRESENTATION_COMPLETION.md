/plan

# MILESTONE 14C — LEGACY PRESENTATION COMPLETION

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read the legacy presentation inventory, all verified feature rows, UI reference
lock, audio architecture and build content policy.

## Objective

Make the complete Phase 1 game visibly and audibly coherent using the sanctioned
Legacy presentation baseline. This is not production remastering.

## Required checks

For every verified player-visible feature ensure:

- required mesh/model exists;
- material/texture is readable and not magenta/missing;
- required temporary rig/animation or logic-driven feedback exists;
- required audio event has a working temporary route/fallback;
- required icon/sign/text is present;
- collider/interaction anchor is usable;
- no greybox remains where an available donor presentation asset exists;
- no donor script/controller/runtime component is present;
- provenance and replacement key exist;
- the feature survives streaming and save/load.

## Allowed work

- sanitized donor import/mapping;
- simple HDRP compatibility materials;
- light/shadow proxy fixes required for readability;
- project-owned wrapper prefabs/presenters;
- missing temporary animation/audio binding;
- obvious import defects.

## Forbidden work

- production mesh/texture reauthoring;
- broad visual redesign;
- Phase 2 vegetation/world replacement;
- new mechanics;
- declaring temporary presentation ProductionReady.

Produce a full Legacy Presentation Coverage report and update the parity matrix.

Stop after 14C.
