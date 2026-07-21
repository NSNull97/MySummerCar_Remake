/plan

# MILESTONE 10A — NPC AND CHARACTER RUNTIME FOUNDATION

Read `AGENTS.md` completely.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- the latest milestone report and review;
- current Git status and diff.

Do not use prompt examples as an exhaustive donor-content list. The locked donor version and parity matrix are authoritative.

Read the locked NPC roster, donor behavior evidence, animation/audio inventory,
world streaming, interaction and save architecture.

## Objective

Build the reusable project-owned NPC runtime required to reproduce the complete
donor roster. This milestone proves the framework with a small explicit fixture
set; it does not populate the full roster.

## Required architecture

Create or align:

- `CharacterDefinition` and `CharacterInstance`;
- stable character IDs independent from donor names;
- legacy character presenter wrapper;
- spawn/home/work/location anchors;
- schedule/time-block model;
- off-screen state progression;
- navigation/route abstraction;
- dialogue definition, condition, line and conversation runtime;
- event/phone/job hooks through project-owned contracts;
- relationship/flags container only to the complexity required by donor parity;
- animation presenter and audio event mapping;
- streamed-cell unload/reload behavior;
- save DTOs, deferred restore and tests;
- character debug inspector and schedule/dialogue trace.

## Legacy presentation

Temporarily bind sanitized donor meshes, rigs, compatible clips, materials and
voice clips through project-owned presenters. Donor AnimatorControllers,
PlayMaker FSMs and scripts are forbidden.

## Fixture proof

Use only a small, explicitly documented fixture set representing distinct NPC
patterns, for example stationary service, scheduled roaming and vehicle-linked
NPC. Do not silently start the full roster.

## Non-goals

- no new advanced utility AI;
- no LLM dialogue;
- no production character reauthoring;
- no enhanced memory beyond donor requirements;
- no full roster population.

Stop after the framework, fixtures, tests and report are complete.
