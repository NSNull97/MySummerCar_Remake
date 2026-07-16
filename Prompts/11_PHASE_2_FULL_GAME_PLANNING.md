/plan

# MILESTONE 11 — PHASE 2 FULL-GAME ROADMAP AND PROMPT GENERATION

Read `AGENTS.md` completely.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

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

## Feature-parity-first rule

The first Phase 2 objective is a playable donor-feature-parity build on the new
foundation, not a broad redesign.

For every donor system, separate:

1. `ParityRequired` — required to reproduce the original playable loop;
2. `TechnicalModernization` — new implementation needed for stability,
   performance, tools, saves, or accessibility without changing the core loop;
3. `RemakeEnhancement` — additional behavior/content after parity;
4. `OptionalExperimental` — may be rejected without blocking parity.

Do not allow an enhancement to replace or delay the parity baseline unless the
user explicitly approves it.

Preserve the fixed guardrails: no physical full-body player, no RPG inventory,
no permanent GPS/quest tracker, and no dependence on Enviro for gameplay state.

## World-cell planning rule

Every remaining world-zone milestone must include:

- real donor canonical captures;
- layout/geometry measurements;
- neutral side-by-side comparison;
- technical validation;
- `ProductionCandidate` status before human review;
- explicit human approval before `Complete`.

Do not plan AI-generated concept art as a substitute for original-game evidence.

## Candidate Phase 2 systems

Audit and prioritize only systems supported by project goals and donor
references, such as:

- player needs, metabolism, perception mixing, sleep, alcohol, and donor-faithful
  survival loop;
- lightweight first-person viewmodel hands for drinking, smoking, driving, and
  approved gestures only;
- physical world items, dynamic trash, sleeping/streamed persistence, crates,
  containers, and recovery;
- economy and money;
- Teimo store, shopping, deliveries, fuel payment, and stock behavior;
- jobs and repeatable work;
- phone/messages, mail, radio, television, and calendar/events;
- inspection, registration, documents, police, fines, jail, and consequences;
- road graph, traffic, driver personalities, recovery, and accidents;
- NPC architecture, schedules, memory, relationships, dialogue, and key
  character scenarios;
- repair/service systems and Fleetari workflow;
- race/rally content;
- water bodies, shoreline, swimming, drowning, buoyancy, boat controller, and
  vehicle flooding;
- mopeds, tractors, vans, boats, and other donor vehicles;
- damage, wear, fluids, electrical, diagnostics, and recovery;
- game-over/permadeath/hospital policy;
- donor-faithful manuals/help without GPS guidance;
- official modding foundation;
- broader world-remaster coverage and reconstruction of remaining voids/legacy
  assets;
- full donor content, balancing, regression, and private beta.

Donor behavior may be technically modernized, but deviations must be explicit,
evidence-based, and separated from the parity milestone.

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

- viewmodel hands and consumable actions;
- needs/metabolism/perception/sleep;
- world items, trash, containers, streaming, and recovery;
- water/swimming/buoyancy/boat/flooding;
- economy and Teimo store;
- full vehicle subsystems;
- traffic foundation;
- NPC foundation;
- key NPC content batches;
- jobs/events/phone/mail/media;
- services/inspection/police;
- damage/wear/fluids/electrical;
- rally and other vehicles;
- remaining world production replacement;
- modding foundation;
- donor-parity validation;
- enhancement backlog after parity;
- private beta build.

Do not execute them.

## Output

Create:

- `Docs/Phase2/PHASE2_SCOPE.md`;
- `Docs/Phase2/DEPENDENCY_GRAPH.md`;
- `Docs/Phase2/MILESTONE_SEQUENCE.md`;
- `Docs/Phase2/RISK_REGISTER.csv`;
- `Docs/Phase2/CONTENT_MATRIX.csv`;
- `Docs/Phase2/DONOR_PARITY_MATRIX.csv`;
- `Docs/Phase2/ENHANCEMENT_BACKLOG.csv`;
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
7. Parity, modernization, enhancement, and experimental scope are separated.
8. The first recommended Phase 2 prompt is named.

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
