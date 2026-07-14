/plan

# CONTEXT REFRESH — NO PROJECT CHANGES

Read `AGENTS.md` completely.

Inspect the current repository, including:

- `Prompts/CURRENT_STATE_AFTER_04.md`;
- current Git branch, status, recent commits, and diff;
- `Packages/manifest.json`;
- `ProjectSettings/ProjectVersion.txt`;
- assembly definitions;
- architecture documentation;
- all milestone reports;
- latest review report;
- current source folders;
- generated/reference folders;
- test assemblies;
- current compiler/test reports when present.

## Objective

Reconstruct an accurate mental model of the project before a new Codex session
continues work.

Do not change any file.

Do not generate code.

Do not update documentation.

Do not run an implementation milestone.

## Required response

Report:

1. Current repository root.
2. Current branch and dirty/clean status.
3. Unity and HDRP version evidence.
4. Latest completed milestone.
5. Current active/incomplete milestone, if any.
6. Main modules and dependency direction.
7. Existing scenes and bootstrap path.
8. Current player/interaction state.
9. Current donor/reference pipeline state.
10. Current world-transfer/remaster state.
11. Current vehicle state.
12. Current tests and last known results.
13. Known blockers and TODOs.
14. Exact next prompt that should be run.
15. Files that the next prompt must read.

Distinguish observed facts from assumptions.

Stop after the context summary.
