/plan

# MILESTONE 10A — FINAL VERTICAL-SLICE POLISH AND RELEASE-CANDIDATE GATE

Read `AGENTS.md` completely.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

Read:

- all milestone reports through 10;
- vertical-slice definition;
- latest build/playthrough reports;
- known-issues list;
- latest review;
- visual regression reports;
- UI/accessibility checklist;
- performance and content-audit reports;
- Enviro integration, quality, vendor-boundary, and environment-owner reports;
- world-cell donor-fidelity comparisons and human approval records;
- current Git status and diff.

## Objective

Turn the verified development vertical slice into a stable private
release-candidate-quality build for testing by the user and friends.

This is a polish and defect-resolution milestone.

Do not add major systems or expand scope.

## Issue triage

Create a single issue table with:

- issue ID;
- severity;
- category;
- reproduction;
- affected build/commit;
- owner/task type;
- fix status;
- verification;
- regression test;
- remaining risk.

Prioritize:

1. crashes/data loss;
2. save corruption;
3. progression blockers;
4. vehicle/physics instability;
5. world collision/streaming blockers;
6. input/UI blockers;
7. severe performance problems;
8. audio failures;
9. visual defects;
10. minor polish.

Do not spend time aligning a decorative screw while save/load is broken.

## Allowed work

- bug fixes;
- regression tests;
- missing error handling;
- UX clarity;
- accessibility fixes;
- UI consistency;
- interaction feedback;
- audio mix corrections;
- material/lighting corrections;
- LOD/pop-in corrections;
- collision fixes;
- measured performance fixes;
- save recovery;
- build packaging;
- documentation.

## Disallowed work

- new vehicles;
- new map regions;
- new NPC systems;
- new quest systems;
- multiplayer;
- major renderer replacement;
- major physics rewrite;
- broad architecture rewrite;
- unplanned content expansion.

If a severe issue requires a major rewrite, document it as a blocker rather than
silently expanding the milestone.

## Polish passes

### Gameplay and interaction

- prompts are clear;
- invalid actions explain why;
- pickup/drop/install flow is consistent;
- no stuck held-object state;
- tool/fastener feedback works;
- controls are discoverable;
- pause behavior is safe.

### Vehicle

- start/drive/stop flow is understandable;
- warnings are visible;
- no common explosive physics;
- reset/recovery is safe;
- audio follows state;
- gauges/HUD agree with telemetry;
- garage/route clearance works.

### World and visuals

- no major floating/embedded assets;
- no blocking seams;
- LOD transitions are tolerable;
- lighting remains readable;
- clear/overcast/rain/storm transitions work without sky/fog flashes;
- rain/wetness/drying works;
- only one sky/cloud/fog/light owner is active;
- Enviro audio remains disabled in production;
- no immediate gameplay lightning after load/spawn;
- interiors have no major leaks;
- reference-only geometry is absent; temporary donor runtime baseline content is present only when the build is explicitly marked private feature-parity and is never mislabelled as final art;
- visual identity remains consistent.

### Donor world fidelity

- every production override cell is recognizable as its original location;
- inactive prototype-rejected custom visuals remain disabled;
- donor baseline regions remain exact until a production override receives explicit approval;
- matched neutral donor/production captures still pass after weather/lighting;
- road approaches, terrain profile, building silhouette, landmarks, clutter and
  vegetation boundaries remain donor-faithful;
- no fog, DOF, grading or foliage hides unresolved structure;
- AI-generated concepts are not used as layout evidence;
- stable world IDs/transforms have not drifted during polish.

A donor-fidelity failure is not a minor visual polish issue. Record it as a
blocking world defect.

### UI and accessibility

- main menu flow;
- loading/error flow;
- settings apply/cancel;
- keyboard/mouse/gamepad;
- focus;
- text/UI scale;
- reduced motion;
- high contrast/readability;
- rebinding;
- confirmations;
- save feedback.

### Audio

- no missing critical event;
- no uncontrolled volume jumps;
- interior/exterior transitions;
- engine state;
- weather and distance-delayed thunder;
- no duplicate Enviro/Wwise rain, wind, lightning, or ambience;
- UI;
- streaming cleanup;
- fallback behavior.

### Save and resilience

- manual save;
- autosave policy;
- backup;
- recovery;
- corrupted-save message;
- relaunch/load;
- settings persistence;
- no state duplication.

## Friend-test packaging

Prepare a private test package consistent with project policy.

Include:

- exact build version;
- commit hash;
- installation/run steps;
- controls;
- known issues;
- save location;
- log location;
- hardware/settings request;
- feedback template;
- crash-report instructions;
- privacy/legal notice appropriate for a private test.

Do not include donor source/extraction/decompiled content. A generated sanitized donor runtime baseline may be included only in an explicitly private local feature-parity test package, with clear non-distribution notice and provenance classification.

Do not present the package as a public release.

## Regression gate

Run:

- full compilation;
- all available tests;
- content audit;
- save round trip;
- clean-install launch;
- new game;
- load game;
- representative playthrough;
- weather/Enviro/lightning/audio/UI;
- performance spot checks;
- reference-only content scan;
- missing-script/reference scan.

Run tests on the final candidate build/config, not only before the last fixes.

## Output

Create:

- `Docs/ReleaseCandidate/ISSUE_TRIAGE.csv`;
- `Docs/ReleaseCandidate/POLISH_CHANGE_LOG.csv`;
- `Docs/ReleaseCandidate/REGRESSION_REPORT.md`;
- `Docs/ReleaseCandidate/WEATHER_ENVIRO_REGRESSION.md`;
- `Docs/ReleaseCandidate/WORLD_DONOR_FIDELITY_REGRESSION.md`;
- `Docs/ReleaseCandidate/PRIVATE_TEST_GUIDE.md`;
- `Docs/ReleaseCandidate/FEEDBACK_TEMPLATE.md`;
- `Docs/ReleaseCandidate/KNOWN_ISSUES.md`;
- `Docs/ReleaseCandidate/RELEASE_CANDIDATE_REPORT.md`;
- `Docs/Milestones/MILESTONE_10A_REPORT.md`.

## Definition of done

1. No unresolved Critical issue.
2. No known save-corruption issue.
3. No vertical-slice blocker.
4. Content audit passes.
5. Final regression suite is recorded.
6. Final build launches.
7. Private test instructions exist.
8. Known issues are honest.
9. Performance remains within the documented accepted range.
10. No major new scope was added.
11. Enviro vendor source is unchanged and production contains no duplicate environment/audio owner.

## Final response

Report:

1. Issues fixed.
2. Issues remaining.
3. Regression results.
4. Performance.
5. Content audit.
6. Final build path/version.
7. Friend-test package.
8. Manual checks.
9. Risks.
10. Go/no-go for private testing.
11. Recommended next planning prompt.

Stop after Milestone 10A.
