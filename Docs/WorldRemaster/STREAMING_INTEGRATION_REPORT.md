# Streaming Integration Report — 05A pilot

## Result

`ProductionWorldCellBuilder` deterministically creates `Production_cell_0_-3.unity` from the registry and project-authored production prefabs. The production root is anchored to the durable home transform and owns its content within one cell; no cross-cell hierarchy or donor object is required.

The same production prefab is integrated into the pilot playtest and existing M05 assembly scene. `WorldRemasterModeController` provides production-only, reference-only and overlay modes in a separate comparison scene. `ProductionCellValidationTool` validates paths, dependencies, cell assignment and build settings.

Two consecutive rebuilds produced identical production-cell SHA-256:

`C0FC69B0C21FE86C1C8435BAE7D47FB3A1A964327DCEFE28DA9AFB773CAFDD52`

## Limits

Only `cell_0_-3` has a generated production scene. Cross-cell roads, water, utility wires, large-object ownership, unload timing and HLOD proxies remain subsequent-zone work. Manual overrides must become explicit project-owned profile data rather than hidden scene edits.

## Batch 01

Вторая production-сцена `Production_cell_0_-2.unity` сгенерирована из `WR_HomeShorelinePier.prefab`, anchored at `(177.67, -1.779, -894.185)` и подтверждена `WorldCellMembershipUtility` как `cell_0_-2`. Два rebuild дали одинаковый SHA-256 `973757687E6A24EF098D32BECB5595B24A2E9A336B77180F821CA64BA7BFD89F`. Cell и её playtest включены в Build Settings; comparison scene исключена. Соседний pilot используется только как review-context, не как runtime dependency production-cell. После ручного finding `SEAM-001` добавлен валидатор минимального геометрического overlap между footpath этой ячейки и terrain соседнего pilot; cross-cell hierarchy при этом не появилась.
