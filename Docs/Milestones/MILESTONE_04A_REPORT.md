# Отчёт Milestone 04A — Bounded World Layout Pilot

Дата: 2026-07-14

Результат: **завершён в заданных границах**. Создан один provenance-aware пилот для гаража и соседнего участка дороги. Полная карта, production terrain, второй world zone, 04B и Milestone 05 не выполнялись. Архитектура, код, prefabs, сцены и input assets Player/Interaction не изменялись.

## 1. Что было прочитано и проверено

- Полностью прочитаны `AGENTS.md` и `Prompts/04A_WORLD_LAYOUT_PILOT.md`.
- Прочитаны обязательные project/architecture/roadmap, donor audit/pipeline/matrix/ledger и актуальные review/fix документы.
- Исходная ветка `main` была чистой; четыре High findings из review закрыты в approved fix report. Открытый `PERF-001` не блокирует metadata/layout pilot и не был расширен до performance milestone.
- `Config/DonorPaths.local.json` подтвердил раздельные donor, staging и Unity project roots.
- Использованы существующие Unity `6000.3.11f1`, HDRP `17.3.0` и AssetRipper `1.3.14`; новые packages, native plugins и внешние зависимости не добавлялись.

## 2. Числовая граница пилота

Donor-world bounds, метры:

- min: `(-269.98, -16.611, 1020.625)`;
- max: `(-49.98, 13.389, 1300.625)`.

Project-local bounds, метры:

- min: `(-100, -15, -20)`;
- max: `(120, 15, 260)`.

Garage anchor donor-world: `(-169.98, -1.611, 1040.625)`.

Преобразование: `projectLocal = donorUnityWorld - garageAnchorDonorWorld`. Обе scene/world системы y-up, одна Unity unit принимается за один метр, перестановки осей нет. Ранее зафиксированное roof mesh-local преобразование `(X,Y,Z) -> (X,Z,Y)` относится только к mesh proof и к scene transforms 04A не применяется.

Allow-list ограничен четырьмя пунктами:

1. `CABIN/Shed` garage anchor hierarchy.
2. `garage_shed_roof` и `garage_shed_walls` как размерный контекст.
3. Только `DirtRoad` waypoints `1655–1685`, семь retained samples.
4. `TERRAIN_OBJ/DirtRoad` render identity только как `Blocked` evidence.

## 3. Инспектированные donor sources

Контейнеры были повторно проверены read-only:

| Donor container | Размер | SHA-256 |
|---|---:|---|
| `mysummercar_Data/level2` | 104,297,292 bytes | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` |
| `mysummercar_Data/sharedassets3.assets` | 533,373,096 bytes | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` |

Точные inspected identities:

- garage: `CABIN` GO `18336` / T `54392` > `Shed` GO `19567` / T `55631` > roof GO `1064` / T `37122`; walls GO `2821` / T `38875`;
- route: `DirtRoad` GO `21869` / T `57925` под `Routes` GO `35620` / T `71667` и `TRAFFIC` GO `30052` / T `66102`;
- retained waypoints: `1655, 1660, 1665, 1670, 1675, 1680, 1685`;
- blocked terrain evidence: `TERRAIN_OBJ` GO `34670` / T `70724`, render `DirtRoad` GO `1616` / T `37675`, candidate combined mesh `TERRAIN_road.001_dirtroad_MeshPart0` PathID `3273`.

Семь samples образуют полилинию длиной `191.353374 m`. Ближайшая retained point к garage anchor — waypoint `1680`, расстояние `204.767727 m`.

`HomeRoad` и второй `garage_shed_walls` instance из другой иерархии были осмотрены только для исключения и не попали в durable data. Полный route, полный scene hierarchy и полный terrain не инвентаризировались.

## 4. External staging и donor guardrails

Внешний metadata-only manifest:

`E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\manifests\MILESTONE_04A_WORLD_LAYOUT_INSPECTION.json`

SHA-256: `ae7a512036f8ade31174e9cb4aee018921755eb1cc7fd1bcfd2707fa8881ac54`.

External logs:

- `MILESTONE_04A_AssetRipper.stdout.log`;
- `MILESTONE_04A_AssetRipper.stderr.log`.

AssetRipper использовался как локальный read-only object inspector. Не выполнялись Unity project export, mass extraction, full scene dump или запись в donor installation. Не экспортировались mesh/texture/material/audio/script/terrain payloads. После инспекции AssetRipper остановлен; финальная проверка не обнаружила процессов Unity или AssetRipper.

Terrain subset классифицирован `Blocked`: доступное static separation ведёт к combined/full-world mesh, а его экспорт нарушил бы границу пилота.

## 5. Реализованный data flow и архитектура

Создан следующий ограниченный путь:

```text
read-only donor inspection
  -> external metadata-only staging manifest
  -> reviewed bounds/transforms/seven samples
  -> project-owned durable JSON
  -> ignored ReferenceOnly comparison
  -> Editor validation + EditMode tests
```

`MSC.World.Runtime` получил только serializable DTOs и чистые функции coordinate conversion/measurement. В runtime нет `UnityEditor`, file I/O, machine paths или donor dependencies.

`MSC.Editor` отвечает за:

- загрузку reviewed JSON;
- проверку schema, allow-list, exact waypoint sequence и provenance;
- canonical SHA-256, external manifest и root separation;
- unique project-owned stable IDs;
- bounds/conversion/measurement tolerance `0.01 m`;
- отсутствие `ReferenceOnly`/`DonorGenerated` dependency у production prefabs и enabled build scenes;
- создание disposable comparison scene.

Donor PathIDs сохранены только как provenance. Zone, source records и samples используют project-owned canonical stable IDs.

## 6. Созданные и изменённые файлы

Новые project-owned assets/code/tests:

- `Assets/Game/World/Runtime/LayoutPilot/WorldLayoutPilotData.cs`;
- `Assets/Game/World/Content/LayoutPilot/M04A_WorldLayoutPilot.json`;
- `Assets/Game/Editor/WorldLayoutPilot/WorldLayoutPilotPaths.cs`;
- `Assets/Game/Editor/WorldLayoutPilot/WorldLayoutPilotValidator.cs`;
- `Assets/Game/Editor/WorldLayoutPilot/WorldLayoutPilotComparisonSceneBuilder.cs`;
- `Assets/Game/Tests/EditMode/WorldLayoutPilot/WorldLayoutPilotTests.cs`;
- соответствующие Unity `.meta` files и folder metas.

Изменённая документация/provenance:

- `Docs/ARCHITECTURE.md`;
- `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`;
- `Docs/ROADMAP.md`;
- `Docs/TESTING_AND_VALIDATION.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/SYSTEM_MAP.md`;
- `Docs/Porting/PORTING_LEDGER.csv`;
- этот отчёт и compatibility pointer `MILESTONE_04A_PILOT_REPORT.md`.

Сгенерированная disposable ReferenceOnly content, намеренно ignored Git:

- `Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity`;
- `Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotMaterials/`.

Production Player, Interaction, M3 garage/road prefabs, build scenes, package manifest и assembly definitions не изменялись.

## 7. Выполненные команды и проверки

Основные операции:

- `Get-FileHash -Algorithm SHA256` для canonical donor containers и external manifest;
- local AssetRipper `1.3.14` read-only web inspection; stdout/stderr записаны во внешний staging;
- `WorldLayoutPilotComparisonSceneBuilder.RunBatch`;
- `WorldLayoutPilotValidator.RunBatch`;
- Unity Test Runner `-testPlatform EditMode`;
- Unity Test Runner `-testPlatform PlayMode`;
- batch validators Foundation, Donor Pipeline, M3 Garage, M4 Player/Interaction и M04A;
- `Import-Csv Docs/Porting/PORTING_LEDGER.csv`;
- `git diff --check`;
- `git diff --name-only -- Assets/Game/Player Assets/Game/Interaction`.

Фактические результаты:

| Проверка | Результат |
|---|---|
| Comparison builder | exit `0`; `M04A_WORLD_LAYOUT_COMPARISON_OK` |
| M04A validator final | exit `0`; `M04A_WORLD_LAYOUT_VALIDATION_OK` |
| EditMode full suite | `54 total`, `54 passed`, `0 failed`, `0 skipped` |
| PlayMode full regression | `5 total`, `5 passed`, `0 failed`, `0 skipped` |
| Foundation validator | exit `0`; stable IDs, asmdef boundaries, paths и Bootstrap valid |
| Donor Pipeline validator | exit `0`; `0 warning(s)` |
| M3 validator | exit `0`; `M3_GARAGE_VALIDATION_OK` |
| M4 validator | exit `0`; `M4_PLAYER_INTERACTION_VALIDATION_OK` |
| CSV parse | success, 51 data rows, 4 new 04A ledger rows |
| Player/Interaction diff | empty |
| `git diff --check` | passed |

Ignored outputs:

- `TestResults/Milestone04A_EditMode.xml`;
- `TestResults/Milestone04A_PlayMode.xml`;
- `Logs/Milestone04A_EditMode.log`;
- `Logs/Milestone04A_PlayMode.log`;
- `Logs/Milestone04A_FinalValidator_*.log`.

Первая 04A validation попытка честно завершилась exit `1`: manifest ошибочно называл waypoint `1670` ближайшим и хранил `214.974451 m`. Validator пересчитал все samples и обнаружил фактический минимум `204.767727 m` у waypoint `1680`. Исправлены только derived metric и SHA external manifest; donor transforms не изменялись. Повторная и финальная валидации прошли.

## 8. Ручная Unity comparison-проверка

Автоматическая генерация и structural validation выполнены. Ручная визуальная проверка в Scene View **не выполнялась** и не заявляется как passed.

Точная процедура:

1. Открыть `Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity`.
2. Выбрать `Comparison_Camera`: position `(0,185,-60)`, target `(0,0,120)`, FOV `54`.
3. Включить одновременно roots `M04A_REVIEWED_LAYOUT_REFERENCE_ONLY` и `M03_CURRENT_PROJECT_OVERLAY_UNCHANGED`.
4. Красная sphere `GarageAnchor_ProjectLocal_Origin` должна совпадать с root M3 garage в `(0,0,0)` с допуском `0.01 m`.
5. Зелёные spheres `Waypoint_1655...1685` и соединяющие segments должны проходить через значения durable JSON; максимальная component delta — не более `0.01 m`.
6. Проверить подпись `Measurements_Route_191.353m_GarageGap_204.768m`.
7. Не ожидать совпадения M3 road с зелёным route: текущий M3 road остаётся у garage prototype, а reviewed samples находятся примерно в `205–260 m` от origin. Это ожидаемое documented mismatch, а не ошибка comparison scene.
8. Не оценивать terrain surface по Y samples как точную высоту: это traffic-route elevation evidence, не terrain heightfield.

Comparison scene отсутствует в Build Settings и находится под `.gitignore`. Её удаление вместе с материалами не должно затронуть production assets; это также проверяет 04A validator.

## 9. Ограничения и риски

1. Donor installation содержит pre-existing mods/extraction artifacts; выводы привязаны к точным локальным hashes и не объявлены clean-stock truth.
2. Retained points принадлежат traffic route, могут иметь lane offset и не являются сертифицированным render-mesh centerline.
3. Waypoint Y — только приблизительное elevation evidence; terrain mesh/heightfield не перенесён.
4. Combined terrain mesh transfer остаётся `Blocked`; обход границы через full-world export не выполнялся.
5. M3 road prefab не соответствует измеренному `204.768 m` garage-to-nearest-sample relationship. 04A документирует расхождение, но не переписывает production content.
6. Garage walls/openings и полный building layout не измерены; garage anchor не является утверждением полной геометрической точности.
7. Ручная визуальная comparison-проверка остаётся невыполненной.
8. Пилот не является complete-world database, production world remaster или началом второй зоны.

## 10. Exit gate

- [x] Один bounded zone и allow-list записаны.
- [x] Все transferred records имеют canonical provenance и project-owned stable IDs.
- [x] Coordinate conversion, bounds, measurements и JSON/manifest round trips тестируются.
- [x] External manifest связан canonical SHA-256 и расположен отдельно от donor/project roots.
- [x] ReferenceOnly content не является production/build dependency.
- [x] Runtime-to-Editor assembly boundary проверена.
- [x] EditMode, PlayMode и все доступные batch validators проходят.
- [x] Нет full-world completeness claim.
- [x] Player/Interaction сохранены без изменений.
- [x] 04B и Milestone 05 не начаты.

## 11. Рекомендуемый следующий milestone

Ровно один следующий milestone: `Prompts/04B_REFERENCE_CAPTURE_AND_MEASUREMENTS.md`, только после отдельного явного запуска пользователем. В рамках этой задачи 04B не выполнялся.
