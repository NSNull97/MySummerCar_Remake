# Building Exterior Report — 05A pilot

## Result

The pilot provides newly authored garage and house shells with foundations, walls, roofs, openings, windows and reusable HDRP material families. Production prefabs are independent from donor meshes and textures.

Direct bindings: `BuildingExterior 15 / 1,985`, `Roof 2 / 160`, `Door 1 / 193`, `Window 1 / 120`. Of all registry records, four bindings are `ProductionCandidate` and twenty are `FirstPass`; none is `Approved` or `Verified` before manual review.

## Fit boundary

The garage is bound to the home anchor and includes a measured pilot clearance of 3.12 m width × 2.22 m height versus the representative 2.20 m × 1.75 m vehicle envelope. Other facade dimensions and silhouettes are reconstructed first-pass geometry and still need measurement/visual review.

Manual work remains for authored topology, UVs, trims, unique landmark details, bake maps, weathering, production LODs and parity captures. Those items are represented in `WORLD_ART_BACKLOG.csv`.

## Batch 01 — hedge boundary

Три `BuildingExterior` hedge records в `cell_0_-2` получили project-authored `WR_HedgeSegment.prefab` на точных converted anchors. Каждый instance имеет simplified collision и двухступенчатый LOD; donor geometry/texture не используются. Привязки имеют `ProductionCandidate`, а silhouette, foliage density и сезонный/weather вид остаются ручной visual gate.
