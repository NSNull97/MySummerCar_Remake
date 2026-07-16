# Аудит существующего извлечения и импорта donor-карты — Milestone 06B1

Дата аудита: 2026-07-16

Политика sanitation: `06B1.4`

Классификация runtime baseline: `TemporaryDirectImport`

Разрешённое распространение: `PrivateLocalFeatureParityOnly`

## Итог

Полная donor-карта уже была извлечена в Milestone 04A1 и не должна
извлекаться повторно. Единственным каноническим источником выбран frozen
AssetRipper export из внешнего staging с revision
`msc-world-baseline-04a1.1-c3f2f337`.

Существующий импорт состоит из двух разных по назначению слоёв:

1. старый локальный `ReferenceOnly`-мир для редакторского сравнения,
   разбитый на 49 ячеек и global-слой;
2. новый детерминированный sanitized runtime baseline
   `World_DonorBaseline_Canonical`, подготовленный для private local
   feature-parity режима.

Ни один из этих слоёв не является production art. Raw extraction,
донорские payload и сгенерированные runtime-baseline assets остаются вне Git.

## Каноническая цепочка

```text
licensed donor installation (read-only historical source)
  -> frozen AssetRipper 1.3.14 export outside Git
  -> WorldTransferExtractor 1.0.0 normalized manifests outside Git
  -> project-owned frozen 04A1.1 tables and static-batch subset table
  -> sanitation policy 06B1.4
  -> ignored World_DonorBaseline_Canonical runtime scene
```

Канонический raw scene:

`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`

SHA-256:

`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`

Подробный source lock находится в
`Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md` и
`Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json`.

## Инвентарь извлечённого мира

| Метрика | Результат |
|---|---:|
| Полные GameObject/Transform placement records | 36 045 |
| Geometry entity records | 13 509 |
| `ReferenceWorldEligible` records | 3 842 |
| Source collider records | 5 001 |
| Bounds-review records | 2 007 |
| Unsupported serialized class IDs | 37 |
| Spatial cells в существующей partition | 49 |
| Global entities | 31 |
| Landmark representatives | 29 |

Все 13 509 geometry records имеют project-owned stable ID. Исключённые из
reference-world записи не потеряны: они остаются в project-owned inventory с
явным статусом `ClassifiedNonWorld`.

## Структура world content

### Terrain, поля и границы мира

- один контекстно подтверждённый `Terrain` record;
- два `Field` records;
- крупный `TERRAINOUT` хранится как global geometry;
- source terrain представлен donor mesh/layout, а не новым `TerrainData`;
- исходные terrain voids, tree-wall границы, under-map поверхности и extreme-Y
  records не исправляются в 06B1.

### Дороги и инфраструктура

- 29 `Road`;
- 2 `Bridge`;
- 163 `RoadSign`;
- 14 `UtilityPole`;
- 4 `Wire`.

Извлечённые mesh и transforms сохраняют видимую donor-сеть, но отдельный
centerline graph, lane topology, shoulder/ditch profile и production road
surface не заявлены.

### Вода и берег

- 12 map-context `Water` records;
- `LAKEBED` и крупные водные/растительные агрегаты относятся к global-слою;
- отдельный shoreline spline и HDRP Water representation отсутствуют;
- donor water meshes сохраняются только как временная геометрическая
  презентация.

### Здания и интерьеры

| Категория | Records |
|---|---:|
| BuildingExterior | 1 568 |
| BuildingInterior | 211 |
| Roof | 124 |
| Floor | 23 |
| Door | 120 |
| Window | 67 |

Transforms и pivots сохранены. Portal graph, floor continuity, gameplay doors,
interactions и interior streaming не переносятся этим milestone.

### Растительность и props

| Категория | Records |
|---|---:|
| VegetationTree | 16 |
| VegetationBush | 7 |
| VegetationGrass | 3 |
| Rock | 3 |
| StaticProp | 265 |
| InteractivePropCandidate | 395 |

Низкое число records для растительности связано с donor aggregate/static-batch
структурой: крупные tree walls и combined vegetation meshes представлены как
агрегаты, а не как отдельный project-owned GameObject на каждое растение.

## Существующий ReferenceOnly import

Локальный импорт Milestones 04A1/05C находится под:

`Assets/Game/LegacyImport/ReferenceOnly/World/Generated/`

Фактическая структура:

- 52 редакторские сцены: bootstrap, persistent, global и 49 cell scenes;
- 503 локальных source mesh assets, около 203 MiB;
- 1 683 детерминированно выделенных static-batch subset meshes;
- neutral category materials и editor/debug presentation;
- `EditorOnly`, без runtime collision и без production/build dependency;
- весь generated payload игнорируется Git и может быть пересобран.

Этот слой уже использовался для полного M05C visual-layout review. Пользователь
принял его как хороший геометрический baseline 2026-07-15. Это историческое
подтверждение исходной карты, но не автоматическая ручная приёмка новой
sanitized 06B1 сцены.

## Canonical sanitized runtime baseline

Выход 06B1:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity`

Generated payload boundary:

`Assets/Game/LegacyImport/RuntimeBaseline/`

Результат политики `06B1.4`:

| Метрика | Результат |
|---|---:|
| Eligible entity metadata | 3 842 |
| Static renderer entities | 2 605 |
| Metadata-only entities | 1 237 |
| Effective-active entities | 2 777 |
| Effective-inactive entities | 1 065 |
| Active renderer entities | 2 123 |
| Inactive renderer entities | 482 |
| Runtime colliders | 0 |

Причины metadata-only:

| Причина | Records |
|---|---:|
| `NoUsableMeshGuid` | 1 058 |
| `SkinnedMeshRendererExcluded` | 62 |
| `CharacterHierarchyExcluded` | 117 |

Character hierarchy исключён намеренно: 06B1 переносит карту, статические
здания, дороги, окружение и props, но не NPC, тела, одежду или аксессуары под
`/skeleton/`.

Renderer payload включает:

- 1 683 derived static-batch subset meshes;
- 922 direct renderer records;
- 446 уникальных direct source mesh copies;
- 22 project-owned neutral `HDRP/Unlit` category materials;
- donor textures не используются;
- shadows, probes и motion vectors отключены для диагностической сцены.

## Transform, scale и bounds

Система координат donor и проекта:

- Y-up;
- оси `X,Y,Z -> X,Y,Z`;
- identity rotation;
- identity scale;
- `1 source unit = 1 project metre`.

Source-to-project translation:

`(169.98, 1.611, -1040.625)`

Canonical scene root остаётся identity:

- position `(0,0,0)`;
- rotation `(0,0,0,1)`;
- scale `(1,1,1)`.

World bounds:

| Space | Min | Max |
|---|---|---|
| Source | `(-3163.1853,-414.4400,-2254.6733)` | `(3383.1853,911.7600,3638.5908)` |
| Project | `(-2993.2053,-412.8290,-3295.2983)` | `(3553.1653,913.3710,2597.9658)` |

Карта не recenter-ится и не интерпретируется художественно. Исторический
garage-roof origin fixture сохранён как coordinate convention.

## Components и runtime logic

Source component IDs сохраняются только как provenance metadata. В canonical
baseline не переносятся:

- donor `MonoBehaviour`;
- PlayMaker FSM;
- old Unity assemblies;
- Rigidbody, joints и gameplay triggers;
- Camera, AudioListener, AudioSource;
- donor light, sky, fog и weather;
- Animation, SkinnedMeshRenderer, Cloth и particle systems;
- UI, save, Steam/platform и gameplay managers.

В scene разрешены только:

- `Transform`;
- принятые `MeshFilter` и `MeshRenderer`;
- project-owned baseline scene/entity metadata;
- одна project-owned neutral directional light и допустимые HDRP light data.

Подробности находятся в
`Docs/WorldBaseline/SANITATION_POLICY.md` и
`Docs/WorldBaseline/SANITATION_COMPONENT_AUDIT.csv`.

## Collision state

В frozen source есть 5 001 collider record:

- 1 469 SphereCollider;
- 1 280 BoxCollider;
- 1 216 CapsuleCollider;
- 1 036 MeshCollider.

В 06B1 runtime collider count намеренно равен `0`. Причины:

- donor triggers нельзя переносить без gameplay intent audit;
- render meshes нельзя автоматически объявлять безопасными MeshCollider;
- duplicate, inactive, extreme и cross-cell colliders ещё не классифицированы;
- continuous traversal и seam-safe ownership относятся к 06B2.

Collider metadata не потеряна. 06B2 должна выбирать только безопасные
кандидаты, сохраняя stable IDs и source provenance.

## Existing streaming state

Существующая project-owned streaming architecture уже поддерживает:

- 512-метровый `cell_X_Z` индекс;
- explicit manifest;
- additive loading;
- load/unload hysteresis;
- owned-scene unload;
- stable-ID validation;
- runtime lifecycle tests.

Текущий active production manifest содержит только две сцены:

- `cell_0_-3`, build index 6;
- `cell_0_-2`, build index 8.

Их streaming infrastructure полезна, но visual content пользователь отклонил
по fidelity. Оба visual слоя классифицированы:

`PrototypeOnly / RejectedForFidelity / Inactive`

Canonical full-map baseline имеет состояние
`PreparedNotActiveUntil06B2`. 06B1 не переключает active profile и не
переписывает streaming architecture.

## Stable identity и gameplay boundary

Каждая из 3 842 baseline entities хранит:

- project-owned stable ID;
- replacement key `legacy-world:<stable-id>`;
- source object ID;
- source parent stable ID;
- source hierarchy path только как provenance;
- source mesh GUID;
- source cell ID;
- semantic category;
- sanitation disposition/reason;
- original `activeSelf` и восстановленный effective-active state.

Gameplay code не имеет права искать эти объекты по donor name или hierarchy
path. Будущие gameplay anchors, saves и overrides должны использовать
project-owned stable IDs, registries и explicit references.

## Известные ограничения

- Временные материалы не воспроизводят donor textures и не являются final HDRP
  materials.
- Runtime collision и безопасный traversal ещё не подключены.
- LOD membership не восстановлено.
- Donor TextMesh presentation исключена.
- Sprite forests, flat proxy fields, tree walls, terrain voids и under-map hacks
  остаются задокументированным legacy visual debt.
- 2 007 bounds-review records требуют осторожности при cell ownership.
- Один Texture2D не был прочитан AssetRipper; он не нужен для geometric
  baseline.
- Свежая ручная проверка именно sanitized 06B1 scene должна быть записана
  отдельно; этот документ её не заявляет.

## Решение аудита

Повторное извлечение не требуется и запрещено текущей стратегией. Existing
frozen extraction пригоден как canonical source для deterministic sanitation.
Baseline допускается только в private local feature-parity режиме.

Следующий технический этап после закрытия 06B1 — детерминированная cellization,
safe collision и active profile в Milestone 06B2.
