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
7. Recommended commit message.
8. Go/no-go for the next prompt.

Stop.
