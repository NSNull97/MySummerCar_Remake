# Git Workflow

## Initial commits

1. `build: create Unity 6 HDRP project`
2. `docs: add project starter kit`
3. `docs: add donor audit and system map`
4. `core: add Unity foundation and bootstrap`

## Branching

For a solo/private project, short-lived milestone branches are enough. Keep `main` buildable.

## Content policy

Never commit:

- donor game binaries;
- raw extracted assets;
- decompiled source dump;
- local path configuration;
- generated Unity cache;
- generated Wwise banks unless a future ADR approves it.

Use Git LFS for newly authored production assets. Verify Git LFS is installed before committing large binaries.

## Commit quality

A commit should have one coherent reason to exist. Include tests/docs with the feature they describe. Avoid dumping an entire milestone into one unreviewable mega-commit when smaller checkpoints are possible.
