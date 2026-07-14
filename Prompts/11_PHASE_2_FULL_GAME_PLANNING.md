/plan

# MILESTONE 11 — PHASE 2 FULL-GAME ROADMAP AND PROMPT GENERATION

Read `AGENTS.md` completely.

Read:

- all milestone reports;
- release-candidate report;
- known issues;
- porting matrix/system map;
- donor audit;
- reference database;
- roadmap;
- architecture;
- player, world, vehicle, weather, audio, UI, and save documentation.

## Objective

Plan the next development phase from a stable vertical slice toward a fuller
private recreation.

Do not implement Phase 2 features in this task.

Produce a dependency-aware roadmap and bounded Codex prompts.

## Candidate Phase 2 systems

Audit and prioritize only systems supported by project goals and donor
references, such as:

- player needs and survival loop;
- economy and money;
- shopping;
- jobs;
- phone/messages;
- calendar/events;
- inspection and registration;
- traffic;
- NPC architecture;
- dialogue;
- police/consequences;
- repair/service systems;
- race/rally content;
- boats/mopeds/tractors/other vehicles;
- damage/wear;
- full fluids/electrical;
- game-over/permadeath policy;
- tutorials/help;
- achievements/statistics;
- modding foundation;
- broader world-remaster coverage;
- full content and balancing.

Do not assume every donor system must be copied exactly.

## Planning requirements

For each proposed milestone include:

- objective;
- user-visible value;
- donor/reference sources;
- architecture dependencies;
- required data;
- required art/audio/UI;
- save impact;
- performance impact;
- test strategy;
- risks;
- explicit non-goals;
- definition of done;
- estimated relative complexity;
- whether it can run in parallel;
- recommended commit/review gate.

Separate:

- core systems;
- content;
- polish;
- optional scope;
- experimental scope.

## Prompt generation

Create a new directory:

`Prompts/Phase2`

Generate bounded prompts, not one giant “make the full game” prompt.

Use numbering and dependencies.

At minimum create planning-ready prompts for:

- needs/economy loop;
- full vehicle subsystems;
- traffic foundation;
- NPC foundation;
- jobs/events;
- services/inspection;
- damage/wear;
- content expansion;
- modding foundation;
- full-game validation;
- private beta build.

Do not execute them.

## Output

Create:

- `Docs/Phase2/PHASE2_SCOPE.md`;
- `Docs/Phase2/DEPENDENCY_GRAPH.md`;
- `Docs/Phase2/MILESTONE_SEQUENCE.md`;
- `Docs/Phase2/RISK_REGISTER.csv`;
- `Docs/Phase2/CONTENT_MATRIX.csv`;
- `Docs/Phase2/PARALLEL_WORK_TRACKS.md`;
- `Docs/Phase2/DEFERRED_AND_REJECTED_SCOPE.md`;
- `Docs/Milestones/MILESTONE_11_REPORT.md`;
- bounded prompts under `Prompts/Phase2`.

## Definition of done

1. The vertical slice is treated as the baseline.
2. Phase 2 scope is explicit.
3. Dependencies are explicit.
4. Optional scope is separated.
5. Prompts are bounded.
6. No Phase 2 implementation occurred.
7. The first recommended Phase 2 prompt is named.

## Final response

Report:

1. Current baseline.
2. Proposed scope.
3. Milestone order.
4. Parallel tracks.
5. Risks.
6. Prompts created.
7. Deferred scope.
8. Exact first Phase 2 prompt.

Stop after planning.
