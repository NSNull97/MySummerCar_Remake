/plan

# MILESTONE CLOSEOUT — NO NEW FEATURES

Read `AGENTS.md` completely.

Determine the milestone explicitly named by the user. If none is named, use the
latest milestone report marked `InProgress`.

If ambiguous, stop and ask for the milestone filename or ID.

## Objective

Close the current milestone without adding new features.

This is a verification/documentation pass, not an implementation expansion.

## Required checks

### Scope

- compare changes against the milestone prompt;
- confirm explicit non-goals were respected;
- identify accidental scope creep;
- identify unfinished required deliverables;
- identify placeholder code falsely presented as complete.

### Compilation

- run Unity batch-mode compilation when configured;
- otherwise provide the exact command;
- distinguish compiler errors, warnings, and unavailable tooling.

### Tests

- run milestone-relevant EditMode tests;
- run milestone-relevant PlayMode tests;
- run project validation tools;
- record exact counts and results;
- do not report unexecuted tests as passed.

### Assets and scenes

- check missing scripts;
- check missing serialized references;
- check duplicate stable IDs;
- check runtime-to-Editor assembly leakage;
- check generated content version stamps;
- check donor/reference leakage into production where relevant.

### Visual/world approval when applicable

- verify real donor/reference fixtures were used;
- verify canonical matched captures exist;
- verify no AI concept was used as layout authority;
- verify inactive prototype-rejected visual content was not reactivated or marked complete;
- verify human approval metadata exists before `Approved`/`Complete`;
- report `AwaitingHumanVisualApproval` when technical checks pass but visual
  approval is absent.

### Vendor boundary when applicable

- record exact installed dependency version evidence;
- record project assemblies that reference vendor types;
- verify vendor source change count;
- verify no paid dependency was silently installed/updated;
- verify sample/demo content and duplicate owners are excluded from shipping.

### Documentation

- ensure the milestone report exists;
- ensure it lists files, commands, tests, manual steps, limitations, and risks;
- update roadmap/status only when supported by evidence;
- ensure ledgers and manifests are current.

### Git readiness

- summarize `git status`;
- summarize changed files by category;
- flag generated or donor files that should not be committed;
- recommend one commit message;
- do not commit automatically unless the user explicitly requested it.

## Allowed changes

Only:

- fixes required to make the completed milestone compile;
- small regression fixes directly caused by the milestone;
- missing tests for required milestone behavior;
- missing report/documentation updates;
- generated-file cleanup required by project policy.

Do not add the next feature.

## Output

Update or create the milestone report and add a `Closeout` section.

Final response:

1. Milestone closed.
2. Scope check.
3. Compilation result.
4. Tests and validation.
5. Missing/manual checks.
6. Git summary.
7. Human visual approval status, when applicable.
8. Vendor-boundary status, when applicable.
9. Recommended commit message.
10. Go/no-go for the next prompt.

Stop.
