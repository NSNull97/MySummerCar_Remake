# Pilot Zone Report — cell_0_-3 Home/Yard

Статус: **production-candidate, automated validation PASS, manual visual review pending**.

## Выбор и anchor

- Zone: `cell_0_-3`, `671` raw reference records.
- Home garage anchor: `(153.495, 0.95, -1033.23)`.
- Rotation: `(0, 180, 0)`; local `+Z` направлен от garage front внутрь donor-aligned yard.
- Source ambiguity with `CABIN/Shed` recorded in `PRE_FLIGHT_AUDIT.md`.

## Реализовано

- `WR_HomeYardTerrain`: deterministic 160 × 130 m authored terrain with flattened home pad and drainage ditch;
- 160 m gravel road, 30 m driveway, ditch-water preview;
- garage shell, two vehicle doors, side door, windows and collision;
- house shell, front door, windows, step and roof;
- garage + representative living/kitchen/sauna interior slice;
- workbench, shelves, crate, drum, mailbox, firewood, two utility poles/wire and two-leaf gate;
- 64 deterministic spruce instances with two visible LOD stages and trunk collision;
- M4 player playtest scene and M05 assembly integration;
- production-only, reference-only and overlay comparison modes.

## Coverage

- Whole registry: `24 / 13 509 = 0.178%` direct production bindings.
- Pilot raw records: `24 / 671 = 3.577%` direct bindings.
- Binding status: `4 ProductionCandidate`, `20 FirstPass`; `0 Approved`, `0 Verified`.
- Pilot production groups are complete for pipeline validation, but raw-object replacement of the whole cell is not complete.

## Main assets

- `Assets/Game/World/Production/Prefabs/WR_HomeYardPilot.prefab`;
- `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity`;
- `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity`;
- `Assets/Game/World/Debug/Comparison/WR_HomeYardComparison.unity`.

Manual gate: open the playtest scene, walk through garage/house doors, open the driveway gate, carry/install M05 parts in `VehicleAssemblyPrototype`, inspect collision and capture the visual matrix.

Automated baseline captures now exist in ignored `PerformanceCaptures/Milestone05A/` and were inspected. The modes switch correctly, but the reference layer contains only sparse metadata proxies, production lighting is dark, and vegetation remains visibly first-pass. Manual in-Editor traversal and art acceptance are still pending.
