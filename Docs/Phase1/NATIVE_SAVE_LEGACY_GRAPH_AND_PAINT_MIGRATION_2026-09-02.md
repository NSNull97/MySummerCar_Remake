# Native save legacy graph and paint migration — 2026-09-02

## Scope

This bounded compatibility fix restores native Satsuma saves written by the
accepted predecessor runtime that contained 126 parts, 117 mounts, 260
fasteners and five renderer-index paint identities. The current generated
runtime contains the same parts and mounts, 280 fasteners, and seven stable
paint surface identities.

No save DTO or stable entity ID was renamed. The source save remains unchanged;
the migrated shape is written only if the player later performs a normal save.

## Migration policy

- The assembly migration accepts only the exact known 260-fastener stable-ID
  set (and the already supported transitive 252-fastener predecessor). The 20
  new exterior-panel fasteners are synthesized; a different missing-ID set is
  rejected even when its count is also 260.
- New exterior fasteners inherit a secured state when their panel was already
  installed. Newly added lower-strut fasteners remain loose, preserving their
  distinct predecessor migration policy.
- Indexed paint migrates only when the save contains exactly
  `legacy-surface-0` through `legacy-surface-4` and every entry exactly matches
  the aggregate whole-car paint state. That state is then applied to all seven
  current stable surfaces.
- Mixed, incomplete, or independently painted legacy surfaces remain a hard
  failure because the old renderer order cannot be mapped without risking
  silent paint corruption.

## Verification

- Exact 260-to-280 assembly migration: 1/1 passed.
- Same-count wrong-stable-ID corruption guard: 1/1 passed.
- Paint migration and rejection guards: 3/3 EditMode passed.
- Private local `slot-01` read-only Bootstrap restore: 1/1 PlayMode passed.
- Portable save, Bootstrap reload, and restore flow: 1/1 PlayMode passed.

The local slot file timestamp remained unchanged after the read-only smoke test.

## Compatibility impact

Current saves remain unchanged. Recognized predecessor saves now load without a
schema bump. Unknown graph changes and ambiguous legacy paint data are still
rejected with an explicit failure instead of being guessed.

Manual in-game confirmation from the Load Game menu remains recommended.
