# Milestone 06B1 — Existing Donor Map Baseline Audit and Sanitization

Дата: 2026-07-16
Unity: `6000.3.11f1`
Политика sanitation: `06B1.4`
Классификация baseline: `TemporaryDirectImport`
Режим распространения: `PrivateLocalFeatureParityOnly`
Итог автоматического gate: **PASS**
Решение для следующего milestone: **GO для 06B2**

## 1. Итог

Повторное извлечение карты не выполнялось. Существующий frozen AssetRipper
export Milestone 04A1 однозначно выбран и зафиксирован как канонический источник.
На его основе создан детерминированный sanitized full-map baseline:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity`

Baseline:

- сохраняет исходные координаты, масштаб, ориентацию и effective-active состояние;
- содержит только разрешённую статическую world presentation и project-owned
  metadata;
- не содержит donor `MonoBehaviour`, PlayMaker FSM, gameplay logic, камер,
  аудио, donor lighting/weather, Rigidbody, joints или runtime assemblies;
- использует отдельную ignored boundary
  `Assets/Game/LegacyImport/RuntimeBaseline/`;
- не включён в обычные Build Settings и не активирован как streaming profile;
- не является production art и не разрешён для публичного/distributable build.

Существующая streaming-архитектура сохранена. Две визуально неточные custom
cells не удалялись и не переделывались; их статус зафиксирован как
`PrototypeOnly / RejectedForFidelity / Inactive`.

## 2. Канонический источник

Source revision:

`msc-world-baseline-04a1.1-c3f2f337`

Канонический frozen source:

```text
rootId: rawExtraction
relative root: raw/world/milestone-04a1
scene:
  assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity
```

Observed local resolution:

```text
E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging
  \raw\world\milestone-04a1
  \assetripper-unity-project
  \ExportedProject\Assets\_Scenes\GAME.unity
```

Ключевые source hashes:

| Артефакт | SHA-256 |
|---|---|
| `GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| `AuxiliaryFiles/path_id_map.json` | `dfdbd71aac8a942c78f27dfd54d73ff3f1fdce3670271482a6fd2c6aa8c73165` |
| Полный source-set fingerprint | `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec` |

Toolchain:

| Инструмент | Версия | Назначение |
|---|---|---|
| AssetRipper | `1.3.14` | Frozen read-only Unity export |
| WorldTransferExtractor | `1.0.0` | Нормализация world manifests |
| WorldPartitionBuilder | `2.1.0-05C` | Существующие reference meshes и static-batch subsets |
| DonorWorldBaselineBuilder | `1.0.0-06B1` | Whitelist sanitation и генерация Unity 6 baseline |

Текущая переустановленная donor-копия не смешивалась с frozen revision:
`sharedassets3.assets` и `sharedassets3.resource` имеют отличающиеся hashes.
Builder до любых generated-payload изменений проверяет frozen scene, path map,
12 normalized manifests и шесть project-owned frozen inputs.

## 3. Existing extraction/import state

До 06B1 уже существовали:

- полный извлечённый donor world и normalized manifests;
- project-owned world database версии `04A1.1`;
- локальный `ReferenceOnly`-слой из 52 editor scenes: bootstrap, persistent,
  global и 49 cell scenes;
- 503 source mesh assets;
- 1 683 детерминированных static-batch subset meshes;
- world partition metadata, landmark/collider tables и stable IDs.

Эта работа повторно не создавалась. `ReferenceOnly`-слой оставлен editor-only.
06B1 добавил отдельное runtime-baseline представление для private feature-parity
режима.

Каноническая source scene содержит:

- 36 045 source placements;
- 13 509 geometry records;
- 3 842 eligible world records;
- 5 001 collider records;
- 49 partition cells;
- 31 global record;
- terrain, roads, bridges, water, vegetation aggregates, buildings, props,
  signs и infrastructure.

## 4. Coordinate contract

| Параметр | Значение |
|---|---|
| Оси | `X,Y,Z -> X,Y,Z` |
| Up axis | `Y` |
| Масштаб | `1 source unit = 1 project metre` |
| Rotation | identity |
| Scale | `(1,1,1)` |
| Translation | `(169.98, 1.611, -1040.625)` |
| Canonical root TRS | identity |

Преобразование позиции:

```text
projectPosition = sourcePosition + (169.98, 1.611, -1040.625)
```

Bounds:

| Space | Min | Max |
|---|---|---|
| Source | `(-3163.1853,-414.4400,-2254.6733)` | `(3383.1853,911.7600,3638.5908)` |
| Project | `(-2993.2053,-412.8290,-3295.2983)` | `(3553.1653,913.3710,2597.9658)` |

Карта не recenter-илась и не интерпретировалась художественно.

## 5. Existing streaming state

Существующая project-owned streaming architecture признана пригодной для
дальнейшего использования:

- `WorldCellIndex` с сеткой `512 m`;
- IDs вида `cell_X_Z`;
- explicit streaming manifest;
- additive scene load/unload;
- load/unload hysteresis;
- owned-scene unload;
- stable-ID и manifest validation;
- explicit focus binding без donor-name lookup.

Активный bounded manifest до 06B2 по-прежнему содержит только:

| Cell | Build index | Текущее назначение |
|---|---:|---|
| `cell_0_-3` | 6 | Технический prototype fixture |
| `cell_0_-2` | 8 | Технический prototype fixture |

06B1 не выполнял full-map cellization, не менял active profile и не
переписывал streaming service. Canonical baseline имеет состояние
`PreparedNotActiveUntil06B2`.

## 6. Sanitation policy и результат

Pipeline использует explicit whitelist. Разрешены:

- `Transform`;
- принятые `MeshFilter` и `MeshRenderer`;
- `DonorWorldBaselineSceneMetadata`;
- `DonorWorldBaselineEntityMetadata`;
- одна project-owned neutral directional light и допустимые HDRP light data.

Запрещены и исключены:

- donor `MonoBehaviour` и PlayMaker FSM;
- donor runtime assemblies и старые `UnityEngine` references;
- камеры, UI, audio, donor lighting, sky, fog и weather;
- player, vehicle, NPC, save, Steam/platform, DRM и gameplay manager logic;
- Rigidbody, joints, WheelCollider, triggers и другие gameplay physics;
- Animation, Animator, SkinnedMeshRenderer, Cloth и character hierarchy;
- ParticleSystem, legacy particles, TextMesh и нерассмотренные LOD data;
- runtime dependency на donor object names или hierarchy paths.

Итоговые counts:

| Метрика | Результат |
|---|---:|
| Eligible entities | 3 842 |
| Renderer accepted | 2 605 |
| Metadata-only | 1 237 |
| `NoUsableMeshGuid` | 1 058 |
| `SkinnedMeshRendererExcluded` | 62 |
| `CharacterHierarchyExcluded` | 117 |
| Effective-active entities | 2 777 |
| Effective-inactive entities | 1 065 |
| Active renderers | 2 123 |
| Inactive renderers | 482 |
| Runtime colliders | 0 |

117 renderer records под `/skeleton/` намеренно исключены: 06B1 переносит
только карту и статическое окружение, а не NPC, тела, одежду или аксессуары.

Effective-active состояние вычисляется по полной 36 045-record hierarchy.
Поэтому 850 records с `activeSelf=true` под inactive ancestor остаются
неактивными после flattening.

## 7. Generated baseline

Canonical hierarchy:

```text
DONOR_WORLD_BASELINE_TEMPORARY_DIRECT_IMPORT
  SANITIZED_STATIC_RENDER_GEOMETRY
  EXCLUDED_SOURCE_METADATA_ONLY
  PROJECT_OWNED_DEVELOPMENT_LIGHTING
    Neutral_Directional_Light
```

Generated payload:

| Тип | Количество |
|---|---:|
| Direct source mesh copies | 446 |
| Derived static-batch subset meshes | 1 683 |
| Всего generated meshes | 2 129 |
| Project-owned neutral HDRP/Unlit materials | 22 |
| Canonical scenes | 1 |

2 605 renderer entities используют 922 direct renderer records и 1 683
derived static-batch records. Source manifest содержит 2 623 проверенных
source/provenance records.

Donor textures не назначаются. Временные материалы используют 22
диагностические категории, GPU instancing и отключённые shadows/probes/motion
vectors. Это presentation для geometry/layout review, а не
`ReauthoredMaterial` или `ProductionReady`.

Scene содержит neutral clear/dry development lighting. Donor sky, fog,
post-processing, weather, cameras и audio отсутствуют.

## 8. Collision state

В source inventory сохранены 5 001 collider record, но runtime baseline 06B1
намеренно содержит `0` colliders.

Автоматическое превращение render meshes в `MeshCollider`, перенос donor
triggers и перенос Rigidbody/joints не выполнялись. Для 06B2 остаются:

- безопасный whitelist статических collider candidates;
- global/cell ownership;
- seam и duplicate validation;
- continuous road/terrain traversal;
- защита от donor voids и out-of-bounds.

Поэтому текущая сцена пригодна для layout/presentation review, но ещё не для
полноценного пешего или автомобильного traversal test.

## 9. Prototype-cell disposition

| Cell | Visual disposition | Сохранено | Отложено до 06B2 |
|---|---|---|---|
| `cell_0_-3` | `PrototypeOnly / RejectedForFidelity / Inactive` | Cell ID, registry, scene, stable IDs, streaming/test infrastructure | Переключение feature-parity profile на donor legacy visuals |
| `cell_0_-2` | `PrototypeOnly / RejectedForFidelity / Inactive` | Cell ID, registry, scene, stable IDs, streaming/test infrastructure | Переключение feature-parity profile на donor legacy visuals |

Custom scenes, prefabs, meshes и materials не удалялись и не
перестраивались. Canonical baseline не зависит от их production visual roots.

## 10. Determinism и source lock

Финальный build выполнен два раза при неизменных source revision и sanitation
policy. Оба запуска завершились `PASS`.

| Fingerprint | Значение |
|---|---|
| Source files | `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec` |
| Semantic scene | `32438aca354e85af6bb8356a0546fc4ac6a0009fb58b5276a97a843191476635` |
| Runtime mesh/material payload | `ac500e0b81db904840b7a6d742db33699548553bf234fb1dfc393bbb56fc7e21` |
| Source manifest file | `E385298C0B6EF344ADE8C3A5F1F7C5FD684B690A8114808D658AEA5F7B15EC6A` |

Manifest SHA-256 после обоих финальных builds полностью совпал. Builder и
validator завершаются failure при source hash drift, count drift, semantic
drift, payload drift, stale generated asset, forbidden component или
dependency leak.

Semantic fingerprint охватывает entity metadata/TRS, canonical roots,
RenderSettings и параметры project-owned neutral directional light.

Generated scene, meshes и materials остаются ignored и не входят в Git. В Git
допускаются только project-owned tools, source manifest, tests, mappings,
metadata и reports.

## 11. Проверки, фактически выполненные

| Проверка | Результат | Evidence |
|---|---|---|
| Unity final compile | PASS | `Logs/M06B1_CompilePolicy064.log` |
| Canonical build, final run 1 | PASS | `Logs/M06B1_BuildCanonical_064_1.log` |
| Canonical build, final run 2 | PASS | `Logs/M06B1_BuildCanonical_064_2.log` |
| Repeated-output determinism | PASS, identical manifest SHA-256 | Оба final build logs и source manifest hash |
| Cold canonical validator | PASS | `Logs/M06B1_ValidateCanonical_064.log` |
| Focused EditMode | `4/4 PASS` | `TestResults/M06B1_EditMode_064.xml` |
| Focused PlayMode scene boot | `1/1 PASS` | `TestResults/M06B1_PlayMode_064.xml` |
| Graphics capture generation | PASS | `Logs/M06B1_CaptureReview_064.log` |
| Raw RuntimeBaseline payload tracked by Git | `0` files | Git boundary audit |

Cold validator подтвердил:

- точную canonical hierarchy и identity root transforms;
- отсутствие missing scripts;
- отсутствие запрещённых component types;
- точные stable IDs, metadata, source parent и activation state;
- корректные mesh/material paths;
- непустые и finite meshes;
- отсутствие donor texture dependencies;
- отсутствие baseline scene в обычных Build Settings;
- отсутствие production/gameplay dependency на runtime-baseline hierarchy;
- отсутствие runtime donor-name/hierarchy lookup.

## 12. Визуальная проверка

Через project-owned capture helper каноническая сцена была открыта и
отрендерена с graphics enabled. Созданы:

- `PerformanceCaptures/Milestone06B1/FullMap_Overhead.png`;
- `PerformanceCaptures/Milestone06B1/HomeShoreline_Oblique.png`;
- `PerformanceCaptures/Milestone06B1/HomeShoreline_Structural.png`;
- `PerformanceCaptures/Milestone06B1/TeimoTown_Structural.png`;
- `PerformanceCaptures/Milestone06B1/capture_manifest.csv`.

Скриншоты были просмотрены ассистентом. По ним подтверждено:

- узнаваемое macro-layout представление полной donor-карты;
- наличие структурной геометрии home/shoreline area;
- наличие структурной геометрии Teimo town/store area;
- наличие дорог, зданий, terrain silhouette и map bounds;
- отсутствие активной custom production reinterpretation в canonical view.

Это **не является пользовательской ручной приёмкой в Unity Editor**. Пользователь
ещё не подтверждал новую sanitized 06B1 scene. Для human sign-off необходимо
открыть:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity`

и проверить в Scene view узнаваемость карты, home/shoreline и Teimo, а также
отсутствие custom prototype visuals. Полноценный traversal check до добавления
collision в 06B2 невозможен.

## 13. Созданные и изменённые артефакты 06B1

Project-owned tooling:

- `Assets/Game/Editor/WorldBaseline/WorldBaselinePaths.cs`;
- `Assets/Game/Editor/WorldBaseline/WorldBaselineSanitationPlan.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineManifest.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineBuilder.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineValidator.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineCommands.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldBaselineCapture.cs`.

Runtime metadata:

- `Assets/Game/LegacyImport/Runtime/DonorWorldBaselineSceneMetadata.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldBaselineEntityMetadata.cs`.

Manifest и boundary:

- `Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json`;
- `Assets/Game/LegacyImport/RuntimeBaseline/.gitkeep`;
- `.gitignore`.

Tests:

- `Assets/Game/Tests/EditMode/WorldBaseline/DonorWorldBaselineEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldBaselinePlayModeTests.cs`;
- `Assets/Game/Tests/PlayMode/MSC.Tests.PlayMode.asmdef`.

Documentation:

- `Docs/WorldBaseline/EXISTING_EXTRACTION_AND_IMPORT_AUDIT.md`;
- `Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md`;
- `Docs/WorldBaseline/CURRENT_STREAMING_ARCHITECTURE.md`;
- `Docs/WorldBaseline/PROTOTYPE_CELL_DISPOSITION.md`;
- `Docs/WorldBaseline/SANITATION_POLICY.md`;
- `Docs/WorldBaseline/SANITATION_COMPONENT_AUDIT.csv`;
- `Docs/WorldBaseline/UNKNOWN_COMPONENTS.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/SYSTEM_MAP.md`;
- `Docs/Porting/PORTING_LEDGER.csv`;
- `Docs/Milestones/MILESTONE_06B1_REPORT.md`.

Локальный generated payload и captures не предназначены для commit.

## 14. Ограничения и риски

- Runtime collision отсутствует; traversal, vehicle route и out-of-bounds safety
  ещё не доказаны.
- Full-map baseline пока не распределён по active streaming layers/cells.
- Large continuous terrain, roads и water требуют seam-safe ownership policy.
- Sprite forests, tree walls, terrain voids, flat proxies и under-map hacks
  остаются документированным legacy visual debt.
- Temporary category materials не воспроизводят donor textures и не являются
  final HDRP art.
- LOD, TextMesh presentation и некоторые inactive alternatives не
  реконструированы.
- Пользовательская ручная Unity Editor приёмка новой sanitized scene ещё не
  выполнена.
- В рабочем дереве присутствуют широкие ранее существовавшие пользовательские
  изменения; 06B1 их не очищал, не откатывал и не включал в свои заявления о
  validation.
- Коммит не запрашивался и не создавался.

## 15. Go / no-go

**GO для `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE.md`.**

Основание:

- canonical source однозначно выбран и зафиксирован hashes;
- существующий extraction/import переиспользован без дублирования;
- sanitized canonical scene создаётся и открывается в Unity;
- transforms, scale и activation state детерминированы;
- forbidden donor runtime logic отсутствует;
- repeated build output совпадает;
- compile, cold validator, EditMode и PlayMode проходят;
- prototype cells имеют явный inactive/rejected disposition;
- существующая streaming architecture пригодна для продолжения.

06B2 должен ограничиться детерминированной cellization, active feature-parity
profile, collider/traversal safety и сохранением global continuous geometry.
Переход к weather или remaster art в рамках этого решения не разрешён.

Milestone 06B1 на этом остановлен.
