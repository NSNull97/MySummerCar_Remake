# Milestone 05B — сводка валидации мира

Дата проверки: 2026-07-15
Версия валидатора: `05B.1`
Проверенный baseline: принятые 05A, 05A Batch 01, 05C и 05C1.

## Решение gate

Проверены все три уровня: `PilotGate`, `VerticalSliceGate` и `FullWorldGate`.
Честно достигнутый уровень — **`None` (ни один gate не закрыт)**.

| Gate | Результат | Блокирующие причины |
|---|---|---|
| `PilotGate` | FAIL | `WORLD-COL-002`, `WORLD-STREAM-001` |
| `VerticalSliceGate` | FAIL | Нет production-маршрута и service-zone, полной геометрической/LOD/streaming-покрытости и текущих runtime performance-измерений |
| `FullWorldGate` | FAIL | 33/3842 eligible bindings, 2/49 production-bound cells, 0 Approved/Verified replacements и незакрытые full-map домены |

Это не означает, что принятый bounded baseline сломан. Пилотные production-ячейки,
статическая spatial parity, collision fixtures и production dependency graph проходят
свои проверки. Формальный `PilotGate` дополнительно требует подключённый production
streaming и полный автоматизированный проход настоящим M4 player controller.

Машиночитаемый источник результата: `WORLD_VALIDATION_RESULT.json`.

## Покрытие

- canonical donor inventory: **33/13 509 = 0.244282%** direct bindings;
- reference-world eligible: **33/3 842 = 0.858928%**;
- production-bound cells: **2/49 = 4.081633%**;
- `cell_0_-3`: **24/671 = 3.576751%**;
- `cell_0_-2`: **9/15 = 60.000000%**;
- остальные 47 concrete cells: **0 direct bindings**;
- Approved/Verified replacements: **0/3 842**;
- 05C reference representation: 2 784 actual meshes и 1 058 явных bounds fallback;
- 05C1 safety topology: 2/2 project-owned pieces, отдельно от donor replacement denominator.

| Eligible category | Covered / total | % |
|---|---:|---:|
| BuildingExterior | 18 / 1 568 | 1.147959 |
| BuildingInterior | 3 / 211 | 1.421801 |
| ColliderOnly | 2 / 772 | 0.259067 |
| Door | 1 / 120 | 0.833333 |
| Roof | 2 / 124 | 1.612903 |
| StaticProp | 4 / 265 | 1.509434 |
| Window | 1 / 67 | 1.492537 |
| Wire | 2 / 4 | 50.000000 |
| Bridge, Fence, Field, Floor, Landmark, Road, Terrain, Water | 0 | 0.000000 |
| InteractivePropCandidate, RoadSign, Rock, SpawnMarker, UtilityPole, Vegetation | 0 | 0.000000 |

Полная разбивка по 24 категориям и всем 49 cells находится в
`WORLD_COVERAGE.csv`. Наличие project-authored terrain/road/water контекста в
пилоте не повышает direct donor-record coverage без проверенного stable-ID binding.

## Spatial parity

Для доступных project-owned fixtures:

- maximum deviation: **0.232306 m**;
- mean deviation: **0.051240 m**;
- nearest-rank p95: **0.232306 m**;
- garage doors: 0.232306 m и 0.200094 m при tolerance 0.35 m;
- home и pier roots: 0 m;
- три hedge anchors: 0 m;
- home/pier overlap: 3.75 m при минимуме 1.0 m;
- pier collider gap: 0.08 m при максимуме 0.10 m;
- 05C1 cross-cell seam: 26 пар, 0 m при tolerance 0.0001 m;
- 05C1 collision probes: 100/100.

Недоступны и не подменены нулевыми результатами: full terrain elevations, road
centerline/width/elevation, junction graph, full shoreline/water level, building
footprints и interior floor levels. Подробности: `SPATIAL_DEVIATION.csv`.

## Production independence

Полный статический dependency graph включает 49 seed assets, 230 посещённых assets
и 482 dependency edges. Запрещённых ссылок на `ReferenceOnly`, `DonorGenerated`
или project Editor content: **0**. Ни одна reference-only scene не включена в
Build Settings. Runtime source не требует внешнего staging path.

Не выполнен отдельный standalone current-world build audit его
`ScriptingAssemblies.json`; это `WORLD-DONOR-002` и блокер только FullWorldGate.

## Streaming, traversal и performance

Два production cells проходят чистый additive load/unload/reload fixture из
`Bootstrap`: два цикла, точные runtime stable-ID sets 7/8, mapped marker counts
24/9, без duplicate IDs и orphan roots. Однако runtime production streamer не
создан и не подключён. `WorldReferenceCellLoader` является runtime-типом; только
его сгенерированный reference-only экземпляр отключён и помечен `EditorOnly`.
В enabled build scenes нет активного non-EditorOnly production
`IWorldStreamingService`, поэтому `WORLD-STREAM-001` блокирует даже PilotGate.

Автоматически подтверждены door/gate clearance, 76-метровый home-to-pier raycast
маршрут, seam/collision 05C1 и отсутствие провала у пирса. Пользователь ранее
подтвердил работу створок, воду, изгородь и bounded ground baseline. Полный маршрут
реальным M4 `CharacterController` и driveable production route не выполнены.

Для текущего мира доступны только статические числа: 370 renderers, 164 colliders,
67 LODGroups и 159 548 instance-counted triangles. Current-world CPU/GPU frame
time, memory, draw calls, VRAM, frame pacing и streaming spikes не измерены. Старый
M3 capture не используется как доказательство 05B.

## Открытые issue IDs

Всего зафиксировано **27** stable finding’ов: **9 закрыто**, **18 открыто**.

Открытые IDs:

`WORLD-BLD-001`, `WORLD-COL-001`, `WORLD-COL-002`, `WORLD-COL-003`,
`WORLD-DONOR-002`, `WORLD-GEO-001`, `WORLD-GEO-002`, `WORLD-GEO-004`,
`WORLD-ID-002`, `WORLD-LOD-001`, `WORLD-PERF-001`, `WORLD-PERF-002`,
`WORLD-PERF-003`, `WORLD-ROAD-001`, `WORLD-STREAM-001`, `WORLD-STREAM-003`,
`WORLD-STREAM-004`, `WORLD-STREAM-005`.

Только `WORLD-COL-002` и `WORLD-STREAM-001` непосредственно блокируют PilotGate.
Все состояния, evidence и required actions записаны в
`WORLD_VALIDATION_ISSUES.csv`.

## Разрешённые 05B-исправления

- Исправлен stale manual status у pilot и Batch 01 без повышения art-статуса.
- Закрыты принятые manual review записи `M05C-GEO-004/005`.
- 05C1 project-owned pieces отделены от 13 509-row donor replacement ledger.
- Исправлен load/unload test fixture, ранее загружавший production cell поверх
  playtest scene с уже встроенной копией той же зоны.
- Добавлены полный dependency graph audit, gate calculator, deterministic exports,
  spatial statistics и `WorldValidationDashboard`.
- `Run all` выполняет шесть underlying validators; canonical export принимает
  только полный Project-result, а zone action явно остаётся issue-filtered
  диагностикой, не самостоятельным formal gate.
- Lifecycle тесты закрепляют точные runtime ID sets 7/8 и marker counts 24/9;
  coverage/gates используют точные множества и иерархические prerequisites.
- Stable-ID jump открывает нужную generated reference scene additively либо
  выделяет source table для excluded record.

Геометрия gameplay layout, broad art, Player/Interaction и Vehicle architecture
в 05B не менялись.

## Go/no-go и следующий шаг

`06_VEHICLE_SIMULATION.md`: **NO-GO**.

Ровно один следующий bounded этап: **05B.1 PilotGate remediation** — подключить
production streaming service через Bootstrap, добавить детерминированный полный
M4 CharacterController traversal fixture для pilot route, затем повторить 05B и
снять current-world performance capture. К 06 переходить только после достижения
`PilotGate`.
