/plan

# CONTINUE THE CURRENT MILESTONE ONLY

Read `AGENTS.md` completely.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

Read:

- the main prompt for the currently active milestone;
- its partial milestone report, if present;
- the latest Git diff;
- the latest test/compile output;
- the latest review report;
- any task log or zone-status file produced by that milestone.

## Determine the active milestone

Prefer, in order:

1. A milestone explicitly named by the user.
2. A report marked `InProgress`.
3. Uncommitted changes clearly belonging to one milestone.
4. The latest incomplete checklist in a milestone report.

If the active milestone cannot be determined safely, stop and ask for the prompt
filename. Do not guess.

## Objective

Continue only the unfinished work of the active milestone.

Do not restart completed phases.

Do not regenerate valid content unnecessarily.

Do not begin the next milestone.

Do not broaden scope because adjacent work looks convenient.

## Procedure

1. Summarize completed versus incomplete checklist items.
2. Re-validate assumptions and paths.
3. Resume from the first incomplete dependency-safe item.
4. Preserve existing architecture unless a demonstrated defect blocks progress.
5. Run focused checks after meaningful changes.
6. Update the milestone report continuously.
7. Leave explicit status for anything still incomplete.

If the active milestone is a world-transfer or world-remaster batch:

- continue only the selected zone or current batch;
- preserve stable IDs;
- update zone status and ledgers;
- do not silently select several additional zones;
- do not reactivate an inactive prototype-rejected custom visual zone;
- for a production-override/remaster batch, do not mark a zone complete without matched donor captures and explicit human approval.

## Completion

When the active milestone is actually complete:

- run its required checks;
- finish its milestone report;
- state that it is ready for human review;
- stop.

Do not invoke the next prompt automatically.
