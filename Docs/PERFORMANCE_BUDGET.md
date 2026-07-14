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
