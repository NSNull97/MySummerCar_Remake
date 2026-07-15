# Milestone 05B — World parity, coverage and integration validation

Дата завершения: 2026-07-15
Статус milestone: **завершён как строгая проверка фактов**
Достигнутый gate: **`None`**
Решение для `06_VEHICLE_SIMULATION.md`: **NO-GO**

## 1. Проверенный scope

Проверено текущее состояние после принятых 05A, 05A Batch 01, 05C и 05C1:

- 13 509-row durable world entity database и production replacement registry;
- все 3 842 eligible reference-world records и 49 concrete cells;
- две production-bound cells `cell_0_-3` и `cell_0_-2`;
- две отдельные 05C1 project-owned safety-topology pieces;
- production prefabs/scenes, enabled Build Settings и полный dependency graph;
- identity, provenance, coverage, spatial parity, streaming lifecycle, traversal,
  collision, art/LOD metadata и доступное performance evidence;
- существующая Player/Interaction и Vehicle Assembly интеграция.

Milestone не создавал broad art, не менял gameplay-critical layout и не переходил
к 06. Donor installation использовалась только для read-only hash recheck; ничего
в ней не изменялось.

## 2. Gate verdict

| Gate | Результат | Основные blockers |
|---|---|---|
| `PilotGate` | FAIL | `WORLD-COL-002`, `WORLD-STREAM-001` |
| `VerticalSliceGate` | FAIL | Нет continuous production route/service area, полной route coverage, runtime performance и production streaming |
| `FullWorldGate` | FAIL | 33/3842 bindings, 2/49 bound cells, 0 Approved/Verified и неполные full-map domains |

Принятый bounded world baseline остаётся валидным в своей заявленной роли.
`PilotGate` не присвоен, потому что прямой additive `SceneManager` lifecycle не
равен подключённому runtime streamer, а raycast/clearance fixtures не равны полному
проходу реальным M4 `CharacterController`.

## 3. Точные результаты

### 3.1 Coverage

- canonical inventory: **33/13 509 = 0.244282%**;
- eligible world: **33/3 842 = 0.858928%**;
- bound cells: **2/49 = 4.081633%**;
- `cell_0_-3`: **24/671 = 3.576751%**;
- `cell_0_-2`: **9/15 = 60.000000%**;
- Approved/Verified replacements: **0**;
- reference actual meshes/fallbacks: **2 784/1 058**;
- 05C1 project-owned safety pieces: **2/2**, вне donor replacement denominator.

По eligible category direct coverage ненулевое только у
BuildingExterior 18/1568, BuildingInterior 3/211, ColliderOnly 2/772, Door 1/120,
Roof 2/124, StaticProp 4/265, Window 1/67 и Wire 2/4. Road, Terrain, Water,
vegetation records и остальные перечисленные в CSV категории имеют 0 direct
stable-ID bindings.

### 3.2 Spatial parity

Измеренный fixture set: max **0.232306 m**, mean **0.051240 m**, p95
**0.232306 m**. Garage doors укладываются в 0.35 m tolerance; home/pier/hedge
anchors совпадают; seam overlap/gap и 05C1 seam/collision проходят.

Full terrain elevation, road centerline/width/elevation, junction graph, full
shoreline/water, building footprint и floor-level parity честно помечены
`Unavailable`, а не `Pass`.

### 3.3 Production independence

Полный статический graph: **49 seeds, 230 assets, 482 edges, 0 violations**.
Production content не зависит от `ReferenceOnly`, `DonorGenerated` или project
Editor assets. Reference-only scenes исключены из Build Settings. Current-world
standalone `ScriptingAssemblies.json` audit остаётся невыполненным.

### 3.4 Streaming

Обе production cells проходят два чистых load/unload/reload cycles из `Bootstrap`
с точными runtime ID sets: 7 IDs и marker count 24 для `cell_0_-3`, 8 IDs и
marker count 9 для `cell_0_-2`, без duplicate IDs и orphan roots. Тип
`WorldReferenceCellLoader` находится в runtime assembly, но его сгенерированный
reference-only экземпляр отключён и помечен `EditorOnly`. Активного non-EditorOnly
production `IWorldStreamingService` в enabled build scenes нет.

### 3.5 Traversal/collision

Door/gate clearance, home-to-pier raycast route, pier gap, 05C1 seam и 100/100
collision probes проходят. Пользовательские manual observations сохранены. Полный
M4 controller traversal и непрерывная driveable route отсутствуют.

### 3.6 Performance

Доступны только статические метрики: **370 renderers, 164 colliders, 67 LODGroups,
159 548 triangles**. Current-world CPU/GPU time, memory, draw calls, VRAM, frame
pacing и streaming spikes не измерены. FPS target не заявляется.

## 4. Добавленное validation tooling

- `Assets/Game/World/Editor/WorldValidationModel.cs` — gate/result/issue/coverage/
  spatial/performance model и gate calculator.
- `Assets/Game/World/Editor/WorldValidationRunner.cs` — all/gate/zone validation,
  реальные underlying validator runs, динамические static metrics, полный
  dependency audit, иерархические gates и deterministic JSON/CSV export.
- `Assets/Game/World/Editor/WorldValidationDashboard.cs` — run all, selected gate,
  issue-filtered selected-zone diagnostics, blockers, coverage, validator runs,
  dependency, stable-ID jump с additive открытием нужной reference scene либо
  выбором source table, overlay, performance locations и latest report actions.
- `Assets/Game/Tests/EditMode/WorldRemaster/WorldValidationEditModeTests.cs` —
  gate, coverage, ID parity, dependency, zone filtering, p95 и export tests.

Machine-readable outputs:

- `Docs/WorldValidation/WORLD_VALIDATION_RESULT.json`;
- `Docs/WorldValidation/WORLD_VALIDATION_ISSUES.csv`;
- `Docs/WorldValidation/WORLD_COVERAGE.csv`;
- `Docs/WorldValidation/SPATIAL_DEVIATION.csv`.

Human-readable outputs:

- `WORLD_VALIDATION_SUMMARY.md`;
- `PRODUCTION_DEPENDENCY_AUDIT.md`;
- `STREAMING_VALIDATION.md`;
- `TRAVERSAL_AND_CLEARANCE.md`;
- `PERFORMANCE_VALIDATION.md`;
- этот milestone report.

## 5. Разрешённые fixes

1. Pilot manual metadata изменена на
   `DoorGatePass;LightingReadabilityLow;FullTraversalPending;PerformancePending`.
2. Batch 01 test ожидание синхронизировано с фактическим status
   `TraversalPass;WaterPass;HedgePass;VisualReferencePending;PerformancePending`.
3. `M05C-GEO-004/005` закрыты как `ClosedManualReview`/`AcceptedManualReview` после
   уже полученного user sign-off.
4. Registry/ledger заново детерминированно сгенерированы: replacement ledger снова
   содержит ровно 13 509 donor records; две 05C1 project-owned pieces учитываются
   отдельно.
5. Production-cell PlayMode fixture переведён с дублирующего playtest context на
   чистый `Bootstrap`, exact runtime stable-ID sets (7/8), exact mapped marker
   counts (24/9) и два lifecycle cycles.
6. Старый hard-coded dependency audit дополнен полным build/production graph.
7. `Run all` теперь действительно выполняет шесть underlying validators; machine
   export принимает только свежий полный `Project` result и не может быть
   перезаписан scoped zone/gate result.
8. Gate calculator и отдельные gate rows сделаны иерархическими; coverage/status
   считает точные множества eligible IDs/cells и Approved/Verified records.
9. Streaming wiring scan отклоняет disabled/inactive и вложенные в `EditorOnly`
   объекты; traversal finding закрывается только выполненным M4 controller test,
   а не строкой manual metadata.

Не исправлялись user-owned `M3_NeutralVolume.asset`, donor hash provenance,
широкая геометрия/арт, Player/Interaction architecture или Vehicle systems.

## 6. Выполненные проверки

- `ProductionCellValidationTool.RunBatch` — PASS,
  `WORLD_REMASTER_05A_VALIDATION_OK warnings=0`;
- `WorldMapGeometryEvaluationValidator.RunBatch` — PASS,
  `eligible=3842 cells=49 meshes=503 actual=2784 fallback=1058`;
- `WorldContinuousGroundBaselineValidator.RunBatch` — PASS,
  `regions=1 pieces=2 vertices=104 triangles=100 seamPairs=26 raycasts=100/100`;
- `WorldValidationRunner.RunBatch` — COMPLETED с code 0 и marker
  `WORLD_VALIDATION_05B_COMPLETED achieved=None bindings=33/3842 openIssues=18`;
  `production-cell`, `geometry-evaluation`, `continuous-ground` и
  `production-cell-identity` — `Pass`; `world-transfer` —
  `KnownProvenanceDrift`; `production-streaming-wiring` —
  `MissingRequiredImplementation`;
- focused WorldRemaster EditMode — **31/31 PASS**;
- focused WorldRemaster PlayMode — **9/9 PASS**;
- full PlayMode regression — **26/26 PASS**;
- full EditMode regression — **134/137 PASS**, три известных несвязанных failure:
  два M3 lighting contract test из-за сохранённого user edit sky type 1 вместо 4;
  один 04A1 donor dry-run из-за hash drift переустановленных
  `sharedassets3.assets/.resource`;
- `git diff --check` — PASS; только informational CRLF/LF warnings для пяти
  generated `Docs/WorldRemaster` files.

Unity batch log содержит восстановившийся transient LicensingClient handshake.
Команда 05B завершилась code 0; состояния внутренних validators перечислены выше
и не маскируются общим словом `PASS`.

## 7. Открытые issues и ограничения

Всего 27 stable issues: 9 закрыты allowed fixes/validation evidence, 18 открыты.
Полный список находится в `Docs/WorldValidation/WORLD_VALIDATION_ISSUES.csv`.

Pilot blockers:

- `WORLD-STREAM-001` — нет production streaming service;
- `WORLD-COL-002` — нет complete real-player traversal fixture.

Остальные open IDs относятся к более высоким coverage/route/full-world gate или
к подготовительным collision/dependency требованиям.

Для принятия самого честного результата 05B дополнительная ручная проверка не
нужна. Для будущего PilotGate потребуются автоматизированный M4 traversal,
runtime streamer transition и current-world performance capture; субъективную
lighting readability следует повторить после отдельного lighting/weather этапа.

## 8. Go/no-go и ровно один следующий milestone

`06_VEHICLE_SIMULATION.md`: **NO-GO** до закрытия PilotGate.

Следующий и единственный рекомендуемый этап: **Milestone 05B.1 — bounded
PilotGate remediation**:

1. установить production `IWorldStreamingService` через Bootstrap;
2. подключить две существующие production cells без name-based lookup;
3. добавить deterministic полный M4 CharacterController traversal fixture;
4. снять current-world performance evidence на доступных locations;
5. повторить 05B и перейти к 06 только при `achievedGate=PilotGate` или выше.

На этом Milestone 05B остановлен.
