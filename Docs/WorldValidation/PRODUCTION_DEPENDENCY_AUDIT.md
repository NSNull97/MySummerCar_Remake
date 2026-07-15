# Production dependency audit — Milestone 05B

Дата: 2026-07-15
Результат статического графа: **PASS**.

## Проверенный graph

В seed set включены:

- весь `Assets/Game/World/Production`;
- две generated production-cell scenes;
- две 05C1 void-fill scenes и их project-owned meshes;
- все девять enabled Build Settings scenes.

Результаты полного `AssetDatabase.GetDependencies` traversal:

| Метрика | Значение |
|---|---:|
| Seed assets | 49 |
| Visited assets | 230 |
| Dependency edges | 482 |
| Forbidden dependencies | 0 |

Запрещёнными считаются runtime/build dependencies на:

- `Assets/Game/LegacyImport/ReferenceOnly/`;
- `Assets/Game/Imported/DonorGenerated/`;
- project-owned `Assets/Game/**/Editor/` content.

Package importer metadata с сегментом `Editor` не считается runtime dependency:
это часть Unity package import graph, а не проектная Editor assembly в player.

## Enabled build scenes

1. `Assets/Game/Bootstrap/Bootstrap.unity`
2. `Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity`
3. `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`
4. `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`
5. `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity`
6. `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity`
7. `Assets/Game/World/Production/Scenes/WorldRemasterHomeShorelinePlaytest.unity`
8. `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity`
9. `Assets/OutdoorsScene.unity`

Reference-only comparison/overview scenes и 05C1 void-fill scenes не включены в
обычный build list. Последние остаются отдельным validated safety baseline, но
ещё не подключены к production streaming (`WORLD-STREAM-005`).

## Что подтверждено

- Production prefabs/scenes не ссылаются на donor meshes или donor textures.
- Reference-only content не нужен для загрузки production assets.
- В runtime C# нет hard-coded donor/staging absolute path.
- Полный граф шире старого hard-coded списка `ProductionCellValidationTool`.
- `WORLD-DONOR-001` закрыт результатом `49/230/482/0`.

## Что не подтверждено

Current-world standalone Windows x64 development build в 05B не создавался.
Поэтому фактический player `ScriptingAssemblies.json` не проверен на Editor DLL;
это открытый `WORLD-DONOR-002`, блокирующий только `FullWorldGate`. Старый M3 build
не считается доказательством текущего world baseline.

Donor installation не удалялась и не менялась ради проверки. Независимость
проверялась безопасным анализом графа, а не физическим удалением reference data.
