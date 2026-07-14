/plan

# READ-ONLY PROJECT REVIEW GATE

Read `AGENTS.md` completely.

Then read:

- `Prompts/CURRENT_STATE_AFTER_04.md`;
- all existing milestone reports;
- architecture, porting, testing, performance, world, player, interaction, and
  vehicle documentation relevant to the current repository;
- package manifests and assembly definitions;
- current Git status and diff;
- the latest completed milestone report.

## Objective

Perform a technical and architectural review of the repository as it exists
right now.

Do not implement fixes.

Do not refactor code.

Do not modify scenes, prefabs, ScriptableObjects, project settings, package
versions, or runtime assets.

The only permitted repository change is creation of the review report described
below.

## Review areas

Inspect at minimum:

### Compilation and project health

- current compiler errors and warnings;
- missing assembly references;
- runtime assemblies referencing Editor assemblies;
- broken serialized references;
- missing scripts;
- package conflicts;
- duplicate types;
- stale generated files;
- invalid defines;
- project settings inconsistent with the current architecture.

### Architecture

- module boundaries;
- dependency direction;
- cyclic dependencies;
- service-locator abuse;
- global mutable state;
- unnecessary singletons;
- giant managers;
- hidden scene lookups;
- `Find*` usage in runtime hot paths;
- static state that can survive Enter Play Mode unexpectedly;
- ownership and lifetime ambiguity;
- simulation mixed with presentation;
- save state coupled directly to scene internals;
- donor import code leaking into runtime.

### Code quality

- oversized classes;
- duplicated logic;
- dead code;
- temporary stubs presented as finished systems;
- TODO/FIXME/HACK markers;
- swallowed exceptions;
- unclear naming;
- public mutable fields;
- fragile string identifiers;
- magic numbers that should be reference/config data;
- incorrect nullability assumptions;
- Unity callbacks with excessive responsibilities.

### Player and interaction milestone

- controller ownership;
- input lifetime;
- camera/input coupling;
- interaction target resolution;
- held-object state;
- physics ownership;
- object pickup/drop edge cases;
- stable-ID integration;
- save readiness;
- allocation risks;
- interaction APIs likely to block vehicle assembly or world streaming.

### Donor/reference pipeline

- donor content accidentally committed;
- raw extraction inside production assets;
- production prefabs depending on donor meshes or textures;
- missing provenance;
- non-idempotent imports;
- hard-coded machine paths;
- potential modification of the donor installation;
- stale or ambiguous manifests.

### Unity/HDRP

- incorrect assembly placement;
- scene bootstrap fragility;
- invalid HDRP settings;
- unnecessary ray-tracing assumptions;
- resources loaded globally without lifecycle control;
- expensive defaults;
- Editor-only code in builds.

### Testing

- missing tests for implemented non-trivial behavior;
- tests that do not assert useful behavior;
- tests coupled to machine paths;
- unexecuted tests falsely reported as passing;
- missing smoke tests for bootstrap/player/interaction.

### Performance and memory

- per-frame allocations;
- repeated component lookups;
- unbounded collections;
- event subscription leaks;
- physics misuse;
- excessive Update methods;
- unnecessary persistent GameObjects;
- synchronous file or hash work in gameplay;
- editor tooling that can freeze or corrupt generated data.

### Documentation and process

- architecture docs inconsistent with code;
- milestone reports missing or inaccurate;
- stale roadmap;
- unrecorded design decisions;
- missing manual steps;
- reports claiming work that cannot be found in the repository.

## Evidence rules

Every finding must include:

- stable finding ID;
- severity;
- confidence;
- affected files and line numbers where practical;
- observed evidence;
- why it matters;
- expected failure mode;
- recommended correction;
- whether it blocks the next milestone;
- suggested verification.

Severity:

- `Critical` — data loss, donor modification risk, project does not compile, or
  architecture makes the next milestone unsafe.
- `High` — likely blocker or expensive defect if ignored.
- `Medium` — should be scheduled, but not necessarily a blocker.
- `Low` — cleanup or improvement.
- `Info` — observation, not a defect.

Use finding ID prefixes:

- `BUILD-`
- `ARCH-`
- `DEP-`
- `PLAYER-`
- `INTERACT-`
- `DONOR-`
- `UNITY-`
- `TEST-`
- `PERF-`
- `DOC-`

Do not invent findings merely to fill categories.

## Commands and tests

You may execute read-only inspection commands.

You may run compilation or tests if the configured Unity Editor is available
and doing so does not change production content.

Do not claim tests passed unless they were actually executed.

Record exact commands and results.

## Output

Determine the latest completed milestone from the repository.

Create:

`Docs/Reviews/REVIEW_AFTER_<LATEST_MILESTONE>.md`

Also create or update:

`Docs/Reviews/LATEST_REVIEW_POINTER.md`

The report must contain:

1. Executive summary.
2. Repository state and Git state.
3. Compilation/test status.
4. Findings grouped by severity.
5. Blocking findings.
6. Non-blocking findings.
7. AGENTS violations.
8. Architecture dependency summary.
9. Player/interaction readiness for world transfer.
10. Donor-pipeline readiness for world transfer.
11. Test coverage gaps.
12. Performance risks.
13. Documentation inconsistencies.
14. Exact recommended fixes by finding ID.
15. Go/no-go recommendation for the next planned milestone.

## Stop condition

Do not fix anything.

Do not begin the next milestone.

Stop after creating the review report and presenting its path.
