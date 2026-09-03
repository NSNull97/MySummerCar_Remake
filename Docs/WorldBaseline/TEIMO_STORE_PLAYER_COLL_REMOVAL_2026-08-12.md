# Teimo store PlayerColl removal — 2026-08-12

## Outcome

The invisible box behind Teimo's shop counter near the sausage refrigerator is
removed from `World_Cell_-3_0_Legacy`. Collision policy `08A1.7` excludes the
same stable collider from subsequent deterministic world regeneration.

## Donor evidence and purpose

- donor hierarchy: `STORE/LOD/ActivateStore/PlayerColl`;
- donor object name: `PlayerColl`;
- donor layer 23: `PlayerOnlyColl`;
- collider: enabled non-trigger `BoxCollider`, approximately
  `2.114 x 2.601 x 3.000 m`;
- entity stable ID: `7d02f149febba255af256479c63313ec`;
- collider stable ID: `e4474f2a451172f949123c2f37727a69`.

The name, player-only physics layer, placement under `ActivateStore`, and broad
box shape identify it as a donor gameplay boundary for the staff side of the
counter. It prevented the player from entering Teimo's work area; it was not
physical collision for the refrigerator, counter, or products. This purpose is
an evidence-based inference because the donor scene contains no explanatory
author comment.

## Compatibility

Only the dedicated player boundary is excluded. Structural shop colliders,
interaction targets, stable entity identity, streaming ownership, save DTOs,
and accepted milestone 00–08A systems are unchanged. No migration is required.

## Validation

Unity 6000.3.11f1 EditMode passed the three focused checks for explicit policy
disposition, locked cellization counts/fingerprint, and absence of the collider
stable ID from the active streamed cell (`3/3`). The broader 06B1 validator is
currently blocked by unrelated pre-existing RuntimeBaseline file-type and
material-contract drift.
