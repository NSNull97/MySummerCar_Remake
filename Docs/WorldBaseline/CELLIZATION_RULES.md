# Правила cellization временного donor world baseline

Дата: 2026-07-16

Milestone: `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE`

Generator: `DonorWorldCellizationBuilder 1.1.0-06B2-v5.1.5`

Source revision: `msc-world-baseline-04a1.1-c3f2f337`

Классификация результата: `TemporaryDirectImport`

Runtime profile: `donor-feature-parity-06b2`

## 1. Назначение

06B2 активирует sanitized donor map из 06B1 как временный private-local
feature-parity baseline и распределяет допустимые статические объекты по
существующей project-owned streaming-сетке.

Cellization не:

- меняет исходные координаты, масштаб, ориентацию или layout карты;
- моделирует заново дороги, terrain, здания, воду или растительность;
- разбивает непрерывные mesh-объекты без проверенного seam-safe инструмента;
- переносит donor scripts, FSM, gameplay, камеры, UI, audio, lighting или
  weather;
- превращает временный baseline в `ProductionReady` art.

## 2. Входные данные и детерминизм

Авторитетные входы:

- canonical sanitized scene 06B1;
- frozen source/manifests revision `msc-world-baseline-04a1.1-c3f2f337`;
- project-owned 512 m partition и исходные `cell_X_Z` assignments;
- explicit safe-collider allowlist;
- project-owned gameplay-anchor manifest.

Ключевые hashes:

| Артефакт | SHA-256 |
|---|---|
| Frozen donor `GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| Gameplay anchors CSV | `4fb51c76ee094872de6d1158bbfeb1e72676668e7cef53cd8fcd4cc1964c65a` |
| Safe collider allowlist CSV | `eb62f26d6b6d9f377e2c9f14c335b163dc8c1246b5aee1f6f81dc4ebebb4ad47` |
| Ownership matrix CSV | `9f83de8770a966ef7cc28ae9be71f00e6668883b9c6e5e2498adf92b8fc93546` |
| Object-to-cell manifest CSV | `794b68c9d86a083343e08452d38ef621bc2c989c433810e7de17429f354cfcb4` |
| Material/texture manifest CSV | `cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192` |

Итоговый ownership fingerprint:

`1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b`

Builder сортирует входы и generated records по стабильным project-owned IDs.
Повторный dry-run/generation обязан воспроизводить тот же fingerprint,
scene set, transforms, replacement keys и ownership.

## 3. Streaming contract

| Параметр | Значение |
|---|---:|
| Cell size | `512 m` |
| Normal loading radius | `1 cell` |
| Unloading radius | `2 cells` |
| Vehicle preload threshold | `12 m/s` |
| Vehicle preload radius | `2 cells` |
| Global scenes | `1` |
| Cell scenes | `49` |

Логическая архитектура из `AGENTS.md` реализована так:

| Логический слой | Реализация 06B2 |
|---|---|
| `World_Global_Legacy` | Одна generated additive scene с непрерывными/global donor objects |
| `World_Cell_<ID>_Legacy` | 49 generated additive scenes из active donor manifest |
| `World_Cell_<ID>_Gameplay` | Project-owned `WorldGameplayCellCatalog`, не зависящий от visual scenes |
| `World_Cell_<ID>_ProductionOverride` | Existing replacement/override infrastructure и стабильные `ReplacementKey` |

Gameplay layer не обязан быть отдельной Unity scene: текущая архитектура уже
держит anchors в отдельном project-owned catalog и загружает visuals независимо.

## 4. Правила назначения ownership

Каждый из 3 842 eligible records получает ровно одного owner.

1. Исходный project transform сохраняется без recentering или художественной
   коррекции.
2. Normal static object сохраняет frozen source cell assignment
   `SourceCellDeterministic`.
3. Объект, уже классифицированный 06B1 как большой/непрерывный, остаётся global.
4. `MAP/MESH` static-batch aggregates остаются global, потому что их
   source pivot/bounds не описывают надёжно реальный cross-cell mesh extent.
5. Явно подтверждённая cross-cell traversal geometry остаётся global.
6. Разрешённые spawn/traversal colliders, которые должны существовать до
   завершения focus-cell load или оставаться непрерывными, остаются global.
7. Geometry не режется и не перемещается между исходными cell coordinates.
8. Empty cell scene сохраняется в manifest, если cell является частью
   канонической 49-cell partition.

Результат:

| Ownership reason | Entities | Renderers | Colliders |
|---|---:|---:|---:|
| `SourceCellDeterministic` | 3 754 | 2 544 | 0 |
| `SourceGlobalLargeOrContinuous` | 31 | 31 | 6 |
| `ExplicitMapMeshAggregateGlobal` | 41 | 29 | 10 |
| `ExplicitCrossCellTraversalGlobal` | 1 | 1 | 1 |
| `BootstrapSpawnOrTraversalCollisionGlobal` | 15 | 0 | 15 |
| **Итого** | **3 842** | **2 605** | **32** |

Global содержит 88 entities. Cell-owned слой содержит 3 754 entities в 46
непустых cells. Три канонические scene остаются пустыми:

- `cell_1_-3`;
- `cell_3_0`;
- `cell_5_-2`.

## 5. Идентичность и replacement

Каждый legacy visual record хранит:

- `LegacyWorldObjectId`;
- project-owned `ReplacementKey` формата `legacy-world:<stable-id>`;
- source object ID и hierarchy path только как provenance;
- source revision;
- global/cell ownership;
- renderer/collider state;
- classification `TemporaryDirectImport`.

Donor object name, donor hierarchy path и Unity instance ID не используются как
save identity или runtime lookup contract.

## 6. Collision policy

06B2 переносит только explicit allowlist из 32 статических colliders:

- 20 `MeshCollider`;
- 12 `BoxCollider`;
- 0 triggers;
- 0 Rigidbody;
- 0 joints;
- 0 donor physics scripts.

Allowlist покрывает bootstrap/home spawn safety и выбранные непрерывные
terrain, road, bridge, rail, field, lakebed и roadside traversal surfaces.
Collider geometry проверяется по vertices, indices и topology после Unity
reserialization; byte-for-byte asset hash не используется как ложный критерий
эквивалентности.

Двери, окна, dynamic props, NPC colliders и trigger volumes намеренно исключены
до соответствующих gameplay milestones.

## 7. Generated boundary

Generated scenes, collision meshes и temporary materials находятся под:

`Assets/Game/LegacyImport/RuntimeBaseline/`

Этот payload игнорируется Git и воспроизводится builder-ом. В Git остаются
только tools, manifests, mappings, metadata contracts, tests и reports.

Private donor baseline разрешён только для локального Development build с
явным process acknowledgement:

`MSC_PRIVATE_DONOR_BASELINE_BUILD=1`

Public/distributable build с donor RuntimeBaseline блокируется pre-build guard.

## 8. Editor workflow

Меню:

`Tools/MSC Remake/World Baseline 06B2/`

Поддерживаются:

- dry-run deterministic plan;
- generation/update active donor profile;
- full validation;
- ownership/cell bounds inspection;
- global/cell scene load and unload;
- legacy/prototype visibility inspection;
- manifest export.

Перед scene open/close tool запрашивает сохранение изменённых scenes.

## 9. Известный технический долг

- Temporary HDRP compatibility materials не являются final production materials.
- Крупные static-batch aggregates остаются global и увеличивают resident memory.
- Safe collider allowlist намеренно узкий.
- Donor terrain voids, sprite forests, flat proxy art и tree-wall boundaries не
  исправляются в 06B2.
- Пользователь принял странности legacy textures, низкое разрешение/полосатость
  terrain и tree-wall artifacts как временный visual debt текущего baseline.
  Это не означает приёмку данных материалов и текстур как production art.
- Physics сообщает предупреждения о шести legacy mesh triangles крупнее 500 m;
  это recorded legacy geometry debt, а не новая ошибка cellization.

## 10. Presentation contract v5.1

Active donor profile по умолчанию использует `LegacyTextured`.
`LegacyDiagnostic` остаётся отдельным режимом проверки, а rejected prototype
visuals остаются `PrototypeHidden`.

- 2 605 renderers сохраняют 2 744 ordered material slots.
- 292 donor material definitions преобразуются в общие project-owned
  `HDRP/Lit` или `HDRP/Unlit` compatibility materials.
- Два renderer-а с built-in material `10302` используют один явный orange
  reviewed fallback.
- 265 source images образуют 269 conversion records: 268 imported
  role-specific texture variants и один donor cubemap, исключённый, поскольку
  sky/reflection/weather ownership не входит в 06B2.
- Sharing contract для source+role variants: `384 -> 268`, что исключает 116
  дублирующих копий.
- Восемь detail-normal variants детерминированно упакованы в HDRP Detail Map
  как `R=.5, G=Y, B=.5, A=X`; donor detail UV и strength сохранены.
- Detail Map назначен 22 материалам: 20 detail-only и двум вместе с primary
  normal.
- Водные материалы сохраняют donor `_BaseColor.a`: `Water4Adv_Lake` использует
  `0.2901961`, а `Water4Simple` — `0.5058824`.
- Shore-foam texture исключена как full-surface base map: этот donor shader
  input не является цветовой текстурой всей поверхности озера.
- Cells используют общие material/texture assets. Runtime применяет только
  `Renderer.sharedMaterials`; material instances не создаются.
- Donor `.shader` files используются только как read-only classification
  evidence и не копируются, не компилируются и не входят в runtime.
- Material/texture presentation fingerprint:
  `e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`.

Повторные генерации подтвердили стабильные material, texture, manifest и
object-to-cell fingerprints. Unity YAML scene serialization не используется
как semantic identity: authority остаётся у stable IDs, source hashes,
ownership/presentation fingerprints и валидатора содержимого.

`M06B2V51_PresentationBuild12_WaterFix.log`,
`M06B2V51_CellizationValidator09_WaterFix.log` и focused EditMode
`M06B2V51_EditMode05_WaterFix.xml` (`7/7`, `65.344 s`), PlayMode
`M06B2V51_PlayMode06_WaterFix.xml` (`6/6`, `7.4557305 s`) и Performance PlayMode
`M06B2V51_PerformancePlayMode06_WaterFix.xml` (`1/1`, `2.5581146 s`) прошли. SHA-256
итогового performance capture:
`60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`.
Короткая ручная перепроверка исправленной воды принята пользователем
2026-07-16: `PASS / HumanAccepted`. В тот же день пользователь прошёл по мостам
и переносил персонажа между ячейками без обнаруженных traversal, collision,
seam, duplicate, popping или load/unload проблем. Bridge/cell-boundary
completion check: `PASS / HumanAccepted`; entry gate 06B3 имеет статус `GO`,
сам 06B3 не начат.
