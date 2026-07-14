# Отчёт buildings and interiors

Semantic layer содержит 1 568 `BuildingExterior`, 211 `BuildingInterior`, 124 `Roof`, 23 `Floor`, 120 `Door` и 67 `Window` records.

| Зона/корень | Eligible geometry | Exterior | Interior | Entities с collider refs | Door/Gate | Статус |
|---|---:|---:|---:|---:|---:|---|
| CABIN — дом/гараж | 98 | 65 | 1 | 52 | 3 | Generated; garage origin automated pass |
| YARD | 614 | 213 | 181 | 295 | 17 | Generated; mini-game branch excluded |
| STORE | 663 | 485 | 1 | 205 | 6 | Generated; dynamic Boxes excluded |
| PERAJARVI | 384 | 265 | 20 | 102 | 19 | Generated; manual town/interior review pending |
| REPAIRSHOP | 339 | 223 | 4 | 125 | 5 | Generated; manual pivot/alignment review pending |
| INSPECTION | 72 | 51 | 0 | 20 | 4 | Generated; no interior proven by current rules |
| COTTAGE | 118 | 65 | 2 | 78 | 1 | Generated; manual island alignment pending |
| LANDFILL | 8 | 2 | 0 | 7 | 0 | Generated; sparse source geometry |
| DANCEHALL | 118 | 52 | 0 | 76 | 3 | Generated; manual review pending |
| WATERFACILITY | 60 | 44 | 1 | 13 | 2 | Generated; manual review pending |
| RYKIPOHJA | 43 | 30 | 0 | 13 | 3 | Generated; manual review pending |

`Interior/Exterior` — semantic evidence, не portal graph. Door/window pivots сохраняются donor transforms, но ещё не проверены визуально. Collider presence указывает только на source components; doorway blockage, floor continuity и convex intent не доказаны.

Все visual replacements имеют статус `DonorReference`/`ReplacementPlanned`; production art в 04A1 не создавался.
