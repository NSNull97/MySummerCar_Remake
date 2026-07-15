# LOD/HLOD Report — 05A pilot

## Implemented

- 64 spruce instances each use an explicit two-stage `LODGroup`.
- Repeated assets share meshes and materials.
- Pilot validation requires non-empty LOD renderers and valid transition ordering.
- Production content remains cell-owned and can later receive cell proxies.

## Not implemented

Building/prop/road LOD chains, billboards, impostors, material simplification, HLOD proxy generation and driving-speed pop validation are not complete. The `Generated/ProductionProxies` location exists for later deterministic proxies, but no false HLOD benefit is claimed.

Current policy is category-specific: close architectural collision persists; vegetation culls/reduces first; landmark exceptions and cell proxies require profiling. The pilot has 64 LOD groups and remains `FirstPass` pending manual pop/shadow review.
