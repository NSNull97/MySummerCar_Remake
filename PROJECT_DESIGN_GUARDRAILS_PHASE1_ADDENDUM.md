# Project Design Guardrails — Phase 1 Full-Game Addendum

## Phase 1 priority

Phase 1 target is not a vertical slice. It is a complete Legacy Feature Complete
build of the locked donor version on the new runtime.

When priorities conflict during Phase 1, use this order:

1. runtime independence from donor code/assemblies;
2. complete donor feature coverage;
3. correctness and saveability;
4. stable IDs and replaceable architecture;
5. Legacy presentation completeness;
6. performance sufficient for full-game testing;
7. production visual quality.

Production reauthoring and remake-only improvements belong to Phase 2 unless
required to make Phase 1 safe or testable.

## Completeness over vertical-slice polish

- Do not polish one route while required donor NPCs, vehicles, jobs or events are
  still absent.
- Do not call a private build full-game when the parity matrix contains missing
  critical rows.
- Do not hide missing gameplay behind UI placeholders, cinematic lighting or
  attractive Legacy world captures.
- Temporary donor presentation is acceptable; missing required content is not.

## Donor behavior authority

- One locked donor version is authoritative.
- Real donor behavior evidence outranks memory, wiki summaries, concept art and
  plausible redesign.
- Prompt examples are category hints, never exhaustive feature lists.
- Fix crashes, softlocks and data loss, but document intentional donor behavior
  differences.

## Replaceability

Every temporary Legacy presenter must be replaceable in Phase 2 without changing:

- stable entity IDs;
- save schema identity;
- event/feature IDs;
- gameplay coordinates and mount points;
- service contracts;
- progression state.

## Phase 2 barrier

Do not implement Phase 2 production art or enhancement backlogs until
`Docs/Phase1/PHASE1_GATE_REPORT.md` contains explicit user approval.
