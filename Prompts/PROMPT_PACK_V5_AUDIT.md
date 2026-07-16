# PROMPT PACK v5 AUDIT — WHAT CHANGED AND WHY

## New user-confirmed facts

- The original MSC map has already been extracted.
- The extracted map has already been visually inspected.
- World streaming has already been connected/prototyped.
- Two custom production cells exist but do not resemble their original
  locations.

The v4 plan incorrectly treated those cells as an immediate art-repair gate.
That would spend time rebuilding two locations while an exact donor map already
exists.

## v5 decision

Use the sanitized exact donor map as a temporary playable runtime baseline for
the feature-parity phase.

```text
extracted donor map
→ sanitize
→ reuse existing streaming
→ active legacy world cells/global scene
→ build gameplay on stable project-owned anchors
→ replace art later through production overrides
```

The two inaccurate custom cell visuals become inactive prototypes. Their useful
streaming infrastructure is retained.

## New milestones

- `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION.md`
- `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md`
- `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`

The old `06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE.md` is now a deprecation stub.

## AGENTS.md changes

The included updated `AGENTS.md` adds:

- a narrowly defined `TemporaryDirectImport` donor-world runtime-baseline
  exception;
- strict sanitation rules;
- private-build versus distributable-build separation;
- legacy/gameplay/production-override layer rules;
- non-destructive cellization rules;
- no donor hierarchy dependency;
- no physical full-body first-person player;
- Enviro 3 as a presentation backend with project-owned weather authority.

Review the diff before replacing the repository root file.

## Weather prompt changes

07A now requires a frozen 06B3 baseline instead of two manually repaired cells.

07C now integrates weather into the active donor-runtime-baseline world and
records partial wetness support for temporary donor materials instead of forcing
a full material remaster.

## What is deliberately postponed

- replacing original terrain;
- rebuilding roads;
- replacing all trees with SpeedTree;
- filling visual terrain voids;
- replacing sprite forests;
- rebuilding flat strawberry beds/ash patches;
- final colliders/LODs/materials;
- side-by-side approval for every production override cell.

These remain remaster/polish work. Immediate traversal and out-of-bounds safety
still must be fixed.

## Correct next sequence

```text
finish + close 06A
→ 06B1
→ closeout/commit
→ 06B2
→ closeout/commit
→ 06B3
→ human baseline check + closeout/commit
→ manual Enviro import if needed
→ 07A
→ 07B
→ 07C
```
