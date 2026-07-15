# Vegetation Remaster Report — 05A pilot

## Result

The pilot uses one newly authored spruce prototype placed 64 times with deterministic seed/positions. Each instance has an `LODGroup` with two mesh stages, shared HDRP bark/foliage materials and simple trunk collision. Repetition uses shared assets; there is no GameObject-per-grass design.

This is a performance/placement proof, not final vegetation. Direct `Vegetation` registry coverage is `0 / 26`; donor placements remain reference records and are linked to the art backlog.

## Deferred production work

- species/biome profiles, deciduous trees, shrubs, grass, reeds and ground litter;
- GPU-instanced terrain details or an approved equivalent at scale;
- wind, billboards/impostors, masks, exclusions, landmark-tree bindings and shadow tiers;
- authored tree topology, UVs, normal/mask maps and visual variation.

The existing 64-tree set is retained as `FirstPass` pilot context only.
