# Правила cellization временного donor world baseline

Дата: 2026-07-16

Milestone: `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE`

Generator: `DonorWorldCellizationBuilder 1.0.0-06B2`

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
| Ownership matrix CSV | `cbe7d5c89ee73b2925f999d9fd2a6dd83968af48bc75e53d28146185b035cc50` |
| Object-to-cell manifest CSV | `3e7cdf966b3b83b920f0d1ee4fb3dc257b75e3cc93542de6c76cf601ce33900d` |

Итоговый ownership fingerprint:

`0a9de0beb45d83d6983153a48d0eb1eb93bb51fdfcc63baa629425f2777b13f3`

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

- Temporary diagnostic materials не являются final HDRP materials.
- Крупные static-batch aggregates остаются global и увеличивают resident memory.
- Safe collider allowlist намеренно узкий.
- Donor terrain voids, sprite forests, flat proxy art и tree-wall boundaries не
  исправляются в 06B2.
- Physics сообщает предупреждения о шести legacy mesh triangles крупнее 500 m;
  это recorded legacy geometry debt, а не новая ошибка cellization.
