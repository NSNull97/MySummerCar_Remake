# Production dependency audit — Milestone 05B.1

Дата: 2026-07-15
Результат статического графа: **PASS**.

## Проверенный graph

Seed set включает весь `Assets/Game/World/Production`, две production-cell scenes,
две 05C1 safety scenes/meshes и все enabled Build Settings scenes.

| Метрика | Значение |
|---|---:|
| Seed assets | 50 |
| Visited assets | 235 |
| Dependency edges | 489 |
| Forbidden dependencies | 0 |

Запрещены production/runtime dependencies на:

- `Assets/Game/LegacyImport/ReferenceOnly/`;
- `Assets/Game/Imported/DonorGenerated/`;
- project-owned `Assets/Game/**/Editor/` content.

Package importer metadata с сегментом `Editor` не считается project runtime dependency.

## Фактический enabled Build Settings order

| Build index | Scene |
|---:|---|
| 0 | `Assets/Game/Bootstrap/Bootstrap.unity` |
| 1 | `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity` |
| 2 | `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity` |
| 3 | `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity` |
| 4 | `Assets/OutdoorsScene.unity` |
| 5 | `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity` |
| 6 | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity` |
| 7 | `Assets/Game/World/Production/Scenes/WorldRemasterHomeShorelinePlaytest.unity` |
| 8 | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity` |

Validator больше не сортирует этот список алфавитно при export. Порядок совпадает
с `EditorBuildSettings`; production streaming manifest проверенно адресует cells
по indices `6` и `8`.

Reference-only overview/comparison scenes и 05C1 safety scenes не включены в
обычный build list. Последние остаются validated bounded baseline, но ещё не
подключены к production streaming (`WORLD-STREAM-005`).

## Подтверждено

- production prefabs/scenes не зависят от donor render meshes или donor textures;
- `ReferenceOnly` и external donor staging не требуются для production loading;
- runtime C# не содержит hard-coded donor/staging absolute paths;
- full graph шире старого hard-coded seed list;
- `WORLD-DONOR-001` закрыт результатом `50/235/489/0`.

## Не подтверждено

Current-world Development Player собран и запущен для performance capture, но его
`ScriptingAssemblies.json` отдельно не проверялся на Editor DLL. Это
`WORLD-DONOR-002`, открытый только для `FullWorldGate`.

Donor installation не изменялась. Независимость проверялась анализом dependency
graph, а не удалением donor/reference content.
