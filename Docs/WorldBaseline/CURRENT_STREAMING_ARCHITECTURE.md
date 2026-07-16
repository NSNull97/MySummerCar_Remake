# Текущая архитектура streaming мира

Дата аудита: 2026-07-16
Milestone: `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION`

## 1. Результат аудита

В проекте уже есть работоспособная ограниченная архитектура additive streaming.
Её не требуется переписывать до интеграции donor baseline.

Существующая реализация:

- использует проектные `WorldCellIndex` и сетку размером `512 m`;
- вычисляет текущую ячейку по позиции явно привязанного focus;
- адресует сцены по build index и проверяет точный project-relative scene path;
- загружает сцены аддитивно;
- использует hysteresis между радиусами загрузки и выгрузки;
- выгружает только сцены, загруженные самим сервисом;
- регистрируется через `IWorldStreamingService`;
- не ищет player, scenes или gameplay objects по donor-именам и hierarchy paths.

Полезная инфраструктура сохраняется для 06B2. В рамках 06B1 active streaming
profile не переключался и canonical donor baseline не распределялся по cells.

## 2. Runtime composition

Текущая runtime-цепочка:

```text
Assets/Game/Bootstrap/Bootstrap.unity
  -> ProductionWorldStreamingInstaller
     -> создаёт M4 player из явно назначенного prefab
     -> передаёт transform player в ProductionWorldStreamingService.BindFocus
     -> регистрирует сервис через GameServiceBindings.CreateWorldStreamingPartial
  -> ProductionWorldStreamingService
     -> читает ProductionWorldStreamingManifest
     -> вычисляет WorldCellIndex
     -> загружает/выгружает additive scenes
```

Основные runtime-файлы:

| Назначение | Путь |
|---|---|
| Общий runtime boundary | `Assets/Game/World/Runtime/IWorldStreamingService.cs` |
| Сетка, ID и bounds cells | `Assets/Game/World/Runtime/Partition/WorldPartition.cs` |
| Тип записи и manifest | `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs` |
| Additive load/unload service | `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs` |
| Explicit Bootstrap composition | `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs` |
| Service bindings | `Assets/Game/Bootstrap/GameServiceBindings.cs` |
| Активный bounded manifest | `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset` |

`ProductionWorldStreamingInstaller` находится на том же process-lifetime
Bootstrap object, что `GameCompositionRoot` и
`ProductionWorldStreamingService`. Focus не разрешается через scene-wide или
name-based lookup: installer создаёт player из сериализованной ссылки и передаёт
его transform непосредственно сервису.

## 3. Cell-index contract

`WorldCellMembershipUtility.FromPosition` использует:

```text
cellX = floor(worldPosition.x / cellSizeMeters)
cellZ = floor(worldPosition.z / cellSizeMeters)
cellId = "cell_<X>_<Z>"
```

Текущий размер cell:

```text
512 m
```

Фактический runtime origin равен `(0, 0, 0)`: поле
`WorldPartitionConfig.worldOrigin` существует как данные конфигурации, но
`FromPosition` его сейчас не применяет. Это является частью текущего поведения,
а не разрешением незаметно сдвинуть donor baseline.

Границы cell по горизонтали:

```text
min = (X * 512, Z * 512)
max = ((X + 1) * 512, (Z + 1) * 512)
```

Расстояние между cell для streaming вычисляется как Chebyshev distance:

```text
max(abs(candidate.X - focus.X), abs(candidate.Z - focus.Z))
```

06B2 должен сохранить этот coordinate contract либо выполнить отдельную
доказанную миграцию. Canonical donor baseline уже находится в проектных мировых
координатах; повторный художественный сдвиг origin недопустим.

## 4. Текущий bounded manifest

`ProductionWorldStreamingManifest.asset` имеет:

- schema version: `1`;
- cell size: `512 m`;
- loading radius: `0`;
- unloading radius: `1`;
- ровно две записи.

| Cell ID | Индекс | Build index | Scene path |
|---|---:|---:|---|
| `cell_0_-3` | `(0, -3)` | `6` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity` |
| `cell_0_-2` | `(0, -2)` | `8` | `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity` |

Build indices соответствуют текущему порядку enabled scenes в
`ProjectSettings/EditorBuildSettings.asset`. Перед каждой загрузкой runtime
проверяет, что build index разрешается ровно в path из manifest. Несовпадение
является явной ошибкой, а не silent fallback.

Manifest валидирует:

- поддерживаемую schema version;
- конечный положительный cell size;
- неотрицательные radii;
- `unloadingRadius >= loadingRadius`;
- непустой список cells;
- уникальные cell IDs, индексы и build indices;
- соответствие `cellId` координатам;
- корректный project-relative `.unity` path.

## 5. Load/unload rules

При `loadingRadiusCells = 0` загружается только запись manifest, индекс которой
совпадает с текущей cell focus.

При `unloadingRadiusCells = 1` ранее загруженная соседняя cell остаётся
резидентной до тех пор, пока Chebyshev distance не станет больше `1`. Это
создаёт ограниченный hysteresis и не допускает немедленной выгрузки на общей
границе.

Сервис хранит собственный набор `ownedLoadedBuildIndices`.

- Если сцена уже была загружена другим владельцем, сервис не присваивает её
  себе.
- При переходе сервис выгружает только scene, build index которой находится в
  owned set.
- `UnloadOwnedScenes` не выгружает чужие заранее загруженные scenes.
- Повторный refresh блокируется флагом `isStreaming`.
- Автоматический refresh запускается только после изменения наблюдаемой cell.

Это ownership-поведение следует сохранить при добавлении global legacy scene,
legacy cells, gameplay layers и production overrides.

## 6. Authoring и validation tooling

| Инструмент | Путь | Текущая роль |
|---|---|---|
| Bounded manifest/Bootstrap builder | `Assets/Game/Editor/WorldStreaming/ProductionWorldStreamingBuilder.cs` | Авторит ровно две 05B.1 fixture-cell и их Bootstrap wiring |
| Strict wiring validator | `Assets/Game/Editor/WorldStreaming/WorldPilotGateRemediationValidator.cs` | Проверяет manifest, Build Settings, Bootstrap и bounded dependencies |
| Lifecycle evidence reader | `Assets/Game/World/Editor/ProductionWorldStreamingLifecycleEvidenceReader.cs` | Читает fingerprinted PlayMode evidence |
| EditMode tests | `Assets/Game/Tests/EditMode/WorldRemaster/ProductionWorldStreamingEditModeTests.cs` | Проверяют manifest validation и exact Bootstrap wiring |
| PlayMode lifecycle | `Assets/Game/Tests/PlayMode/WorldRemaster/WorldRemasterPlayModeTests.cs` | Проверяет два load/unload цикла, ownership и stable-ID snapshots |
| Durable evidence | `Docs/WorldValidation/M05B1_PRODUCTION_STREAMING_LIFECYCLE.json` | Результат принятого bounded lifecycle test |

Принятая историческая проверка покрывает:

- два полных цикла `pilot -> pilot+next -> next -> none`;
- owned scene counts `1 -> 2 -> 1 -> 0`;
- уничтожение roots после owned unload;
- отсутствие duplicate stable IDs;
- exact stable-ID snapshots: `7` ID для `cell_0_-3` и `8` ID для
  `cell_0_-2`;
- fingerprints manifest, Bootstrap и streaming implementation.

Эти результаты относятся к двум техническим prototype scenes. Они доказывают
streaming lifecycle, но не доказывают donor-map fidelity и не являются
валидацией canonical donor baseline.

## 7. Что переиспользуется

Следующие части сохраняются как project-owned infrastructure:

1. `WorldCellIndex`, ID format и `512 m` grid.
2. `WorldCellMembershipUtility` и bounds helpers.
3. Manifest validation и exact build-index/path contract.
4. Explicit focus binding из Bootstrap.
5. Additive loading.
6. Hysteresis между load/unload radii.
7. Owned-scene accounting и безопасная выгрузка.
8. `IWorldStreamingService` boundary и composition-root registration.
9. Stable cell IDs `cell_0_-3` и `cell_0_-2`.
10. Project-owned stable IDs, registry records и lifecycle fixtures.
11. Existing validators, tests и fingerprinted evidence как regression
    baseline.

## 8. Что не является active donor streaming

Canonical sanitized baseline расположен по пути:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity`

В 06B1 эта scene:

- не добавлялась в active production streaming manifest;
- не включалась в Build Settings;
- не заменяла текущие cell scenes;
- не разбивалась физически по cells;
- не подключалась к Bootstrap.

Следовательно, наличие canonical scene не означает, что full donor map уже
streamed или активен в feature-parity runtime.

Старый `WorldReferenceCellLoader` в
`Assets/Game/World/Runtime/Streaming/WorldReferenceCellLoader.cs` также не
является active production authority. Он адресует scenes по именам, имеет иной
configuration contract и не подключён текущим Bootstrap. Продвигать его как
06B2 solution без отдельного обоснования нельзя.

## 9. Ограничения текущей реализации

1. Active manifest содержит только две cells из полной карты.
2. Обе текущие scene являются визуально отклонёнными custom prototypes.
3. Сейчас нет отдельного feature-parity profile для donor baseline.
4. Нет `World_Global_Legacy` scene ownership contract.
5. Нет `World_Cell_<ID>_Legacy` catalog для полной карты.
6. Нет явной runtime-композиции
   `Legacy / Gameplay / ProductionOverride`.
7. Нет replacement-key switch между legacy renderer/collider и production
   override.
8. Continuous terrain, roads, water и большие cross-cell meshes ещё не
   классифицированы как global или cell-owned runtime payload.
9. Full-world persistence, memory budget, streaming hitch budget и vehicle
   boundary traversal не доказаны.
10. Текущие PlayMode snapshots жёстко описывают prototype-cell content и не
    должны ошибочно стать требованием к donor baseline hierarchy.
11. `ProductionWorldStreamingBuilder` жёстко авторит только две 05B.1 scenes;
    его нельзя бездумно запускать как full-map/profile builder.
12. Build/distribution guard для временного donor baseline должен оставаться
    явным: baseline разрешён только для private local feature-parity builds.

## 10. Контракт перехода в 06B2

06B2 должен расширить существующую архитектуру, сохранив её проверенные
свойства. Требуемый logical result:

```text
World_Global_Legacy
World_Cell_<ID>_Legacy
World_Cell_<ID>_Gameplay
World_Cell_<ID>_ProductionOverride
```

Минимальные требования к active-profile переключению:

- профиль выбирается project-owned configuration, а не donor hierarchy name;
- feature-parity profile активирует sanitized donor baseline layers;
- prototype fixture profile может явно активировать старые custom scenes только
  для development tests;
- `cell_0_-3` и `cell_0_-2` сохраняют свои cell IDs;
- custom visual roots не загружаются как feature-parity visuals;
- project-owned gameplay metadata не теряется вместе с отключением visuals;
- legacy и production override не создают duplicate renderers/colliders;
- large continuous objects остаются global до появления seam-safe cellization;
- failure не должен silently возвращать rejected prototype visuals.

Фактическая реализация этого переключения относится к 06B2. В 06B1 manifest,
Bootstrap, scenes и runtime activation не изменялись.

## 11. Источники аудита

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/WorldValidation/STREAMING_VALIDATION.md`;
- `Docs/Milestones/MILESTONE_05B1_REPORT.md`;
- `Docs/Milestones/MILESTONE_06B_REPORT.md`;
- `Docs/WorldFidelity/REJECTED_CELL_IDENTIFICATION.md`;
- runtime, Editor и test-файлы, перечисленные выше;
- `ProjectSettings/EditorBuildSettings.asset`;
- `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`.
