/plan

# READ-ONLY PROJECT REVIEW GATE

Read `AGENTS.md`, current state, guardrails, Phase 1 scope/matrix, reports,
architecture, manifests, Git state and latest completed milestone.

Do not implement fixes. Only create the review report.

Review compilation, architecture, donor/runtime separation, world baseline,
NPC/vehicle/item/service/event completeness, parity-matrix truthfulness,
save coverage, temporary presentation/provenance, UI reference lock, Enviro and
audio boundaries, tests, performance and documentation.

Use finding prefixes including:

```text
BUILD ARCH DEP DONOR PARITY NPC VEHICLE ITEM JOB STORY SAVE UI AUDIO WORLD PERF TEST DOC
```

Every finding needs ID, severity, confidence, evidence, affected paths, failure
mode, correction, blocker status and verification.

Create `Docs/Reviews/REVIEW_AFTER_<MILESTONE>.md` and update latest pointer.
Stop without fixes.
