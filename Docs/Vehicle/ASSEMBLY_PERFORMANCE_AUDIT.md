# M05 Assembly Performance Audit

Профиль: Unity Editor batch mode, deterministic rear-drum candidate query.

- Iterations: `10000`
- Total query time: `262,093 ms`
- Mean: `26,209 µs/query`
- Managed allocation in measured loop: `0 bytes`
- Graph mutations during query loop: `0`
- Scene GameObjects: `115`
- Renderers: `59`
- Colliders: `45`
- Rigidbodies: `15`
- Candidate search uses serialized arrays and `OverlapSphereNonAlloc`; no scene-wide lookup is performed per frame.
- Dependency graph changes only after successful operations or restore, not during preview queries.
