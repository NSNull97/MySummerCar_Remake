# Initial Performance Budget

These are starting targets, not sacred numbers. Measure and revise through ADRs.

## Baseline

- Windows x64.
- 1920x1080.
- Stable 60 FPS on a mid-range gaming PC.
- No ray-tracing requirement.

## CPU

- Avoid per-frame managed allocations in hot gameplay loops.
- Vehicle simulation substeps must be configurable.
- Distant loose rigidbodies sleep or use simplified state.
- Slow systems use scheduled ticks.
- Avoid global object searches during gameplay.

## GPU

- Use LODs for production environment and vehicle assets.
- Control shadow distances and caster counts.
- Use sensible vegetation density and impostors/billboards where appropriate.
- Share materials and use instancing where practical.
- Avoid excessive transparent layers and full-screen effects.

## Memory and content

- Reference-only donor assets excluded from builds.
- Streaming cells or additive scenes for the larger world.
- Texture resolution chosen by texel density and screen use, not ego.
- Audio banks organized by domain/streaming needs later.

## Required captures

For each major vertical slice record:

- CPU main thread;
- render thread;
- GPU frame time;
- managed allocations;
- physics time;
- visible triangles/draw calls;
- memory snapshot;
- build size;
- tested hardware and settings.

## Post-Milestone 4 review-fix status

The `DEP-001` correction moved the M3 opt-in capture component from
`MSC.World.Runtime` to `MSC.Development.Performance`. The complete capture and
file-output branch is compiled only for the Editor or a development player; a
release player retains only an inert serialized-reference placeholder.

Validation on 2026-07-14 rebuilt the Windows x64 development player and reran
the M3 1920×1080 High Fidelity capture on an AMD Ryzen 9 5950X and NVIDIA RTX
4070 SUPER (Direct3D11):

- build size: 217,320,758 bytes;
- 600 measured frames after 180 warmup frames;
- mean frame time: 2.876 ms;
- p95 frame time: 3.250 ms;
- mean CPU frame time: 2.748 ms;
- GPU frame timing: unavailable (`0.0 ms`) despite 600 frame-timing samples.

Ignored artifacts are stored under `PerformanceCaptures/Review99A/` and
`Logs/99A_Performance*.log`.

This rerun validates the development tooling move but does **not** close the
required-capture gate. It is still an M3 garage-only scene and lacks a reliable
GPU time, managed-allocation series, physics time, draw-call/visible-triangle
runtime series and memory snapshot. A representative integrated Player + World
slice does not yet exist; creating one during the review-fix task would cross
the approved milestone boundary. `PERF-001` therefore remains explicitly
blocked rather than being reported as fixed.
