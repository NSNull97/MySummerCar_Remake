/plan

Read `AGENTS.md`, prior reports, `Docs/SAVE_SYSTEM.md`, donor audit, and save-format findings.

Complete **Milestone 9 only: save hardening and optional donor-save study**.

Implement:

- versioned native `SaveDocument`;
- atomic write and backup;
- stable-entity state collection/application;
- migrations;
- corruption/error reporting;
- tests for round trip, migration, backup, and failure handling.

Study donor saves only if their location/format is safely understood. Any donor importer must be isolated:

```text
Donor DTO -> validated conversion -> native SaveDocument
```

Never modify the donor save. Preserve source files and produce a conversion report.

If the format is unclear, document findings and stop short of guessing.

Create `MILESTONE_09_REPORT.md` and stop.
