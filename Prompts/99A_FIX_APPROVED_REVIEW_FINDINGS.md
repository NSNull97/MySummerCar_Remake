/plan

# FIX ONLY EXPLICITLY APPROVED REVIEW FINDINGS

Read `AGENTS.md` completely.

Read the latest report referenced by:

`Docs/Reviews/LATEST_REVIEW_POINTER.md`

Also read the user message that invoked this prompt.

## Required user input

The user must explicitly provide approved finding IDs, for example:

```text
Approved finding IDs: ARCH-001, TEST-003
```

If no finding IDs are explicitly provided, stop without changing files and ask
for the IDs.

Do not infer approval from severity.

## Objective

Fix only the explicitly approved findings.

Do not opportunistically clean unrelated code.

Do not cross milestone boundaries.

Do not begin the next milestone.

## Rules

For each approved finding:

1. Re-verify that the finding still exists.
2. Identify the smallest safe correction.
3. Preserve public APIs unless the finding requires a documented API change.
4. Preserve stable IDs and serialized data.
5. Do not modify donor files.
6. Do not install packages silently.
7. Add or update a regression test when practical.
8. Update affected documentation.
9. Record any behavior change.
10. Stop and report rather than guessing when the evidence is insufficient.

If one approved finding depends on an unapproved broader rewrite, do not perform
the broader rewrite. Mark that finding blocked and explain why.

## Validation

Run the smallest relevant checks after each fix.

At the end, run:

- compilation when Unity batch mode is available;
- relevant EditMode tests;
- relevant PlayMode tests;
- static assembly-boundary checks;
- missing-reference validation where available.

Do not claim execution when tools are unavailable.

## Output

Create:

`Docs/Reviews/FIX_REPORT_<TIMESTAMP>.md`

Update the original review report with a clearly separated resolution table:

- finding ID;
- status;
- files changed;
- tests;
- residual risk;
- commit recommendation.

Final response:

1. Approved IDs received.
2. Findings fixed.
3. Findings blocked.
4. Files changed.
5. Commands/tests executed.
6. Results.
7. Remaining risks.
8. Whether the next milestone is now safe.

Stop after the approved fixes.
