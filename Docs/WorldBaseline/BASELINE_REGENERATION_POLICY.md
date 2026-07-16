# Политика регенерации donor world runtime baseline

Дата: 2026-07-17

Область: sanitized temporary donor world baseline для private-local
feature-parity разработки.

Классификация результата: `TemporaryDirectImport`.

Режим распространения: `PrivateLocalFeatureParityOnly`.

Статус документа: нормативная политика. Этот документ сам по себе не является
доказательством того, что reproducibility check Milestone 06B3 уже выполнен.

## 1. Назначение

Регенерация baseline разрешена только как детерминированное воспроизведение
зафиксированного project-owned контракта. Она не используется для:

- повторного извлечения карты из текущей donor-установки;
- художественной правки геометрии, координат или layout;
- исправления donor visual debt;
- добавления donor runtime scripts, FSM, камер, UI, audio, lighting или weather;
- перезаписи уже замороженной baseline revision другим содержимым под тем же ID.

Сгенерированный payload остаётся воспроизводимым локальным артефактом под
`Assets/Game/LegacyImport/RuntimeBaseline/` и не коммитится.

## 2. Зафиксированный входной контракт

| Поле | Значение |
|---|---|
| Baseline revision | `DonorWorldBaseline-v001` |
| Freeze state | `Frozen` |
| Source revision | `msc-world-baseline-04a1.1-c3f2f337` |
| Source classification | `TemporaryDirectImport` |
| Frozen `GAME.unity` SHA-256 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| Source-set fingerprint | `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec` |
| Sanitation policy | `06B1.4` |
| Active runtime profile | `donor-feature-parity-06b2` |
| Cellization generator | `1.1.0-06B2-v5.1` |
| Presentation generator | `06B2-v5.1.5` |
| Ownership fingerprint | `1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b` |
| Ownership matrix SHA-256 | `9f83de8770a966ef7cc28ae9be71f00e6668883b9c6e5e2498adf92b8fc93546` |
| Object-to-cell manifest SHA-256 | `794b68c9d86a083343e08452d38ef621bc2c989c433810e7de17429f354cfcb4` |
| Material/texture manifest SHA-256 | `cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192` |
| Presentation fingerprint | `e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd` |
| Unity | `6000.3.11f1` |

Координатный контракт:

| Поле | Значение |
|---|---|
| Axes | `X,Y,Z -> X,Y,Z` |
| Up axis | `Y` |
| Units | `1 source unit = 1 project metre` |
| Rotation | identity `(0,0,0,1)` |
| Scale | `(1,1,1)` |
| Translation | `(169.98,1.611,-1040.625)` |
| Project bounds min | `(-2993.2053,-412.8290,-3295.2983)` |
| Project bounds max | `(3553.1653,913.3710,2597.9658)` |

Состав активного baseline:

- одна global legacy scene;
- 49 cell legacy scenes;
- 3 842 eligible entities;
- 2 605 renderers;
- 32 явно разрешённых статических collider;
- 15 project-owned gameplay anchors.

Авторитетные project-owned записи:

- `Assets/Game/LegacyImport/Manifests/DonorWorldBaselineSourceManifest.json`;
- `Assets/Game/LegacyImport/Manifests/DonorWorld06B2PresentationManifest.json`;
- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2SafeColliderAllowlist.csv`;
- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2GameplayAnchors.csv`;
- `Docs/WorldBaseline/GLOBAL_AND_CELL_OWNERSHIP_MATRIX.csv`;
- `Docs/WorldBaseline/LEGACY_OBJECT_CELL_MANIFEST.csv`;
- `Docs/WorldBaseline/LEGACY_MATERIAL_TEXTURE_MANIFEST.csv`.

## 3. Инструментальный контракт

| Инструмент | Версия | Роль |
|---|---|---|
| AssetRipper | `1.3.14` | Исторический read-only frozen export |
| WorldTransferExtractor | `1.0.0` | Детерминированная нормализация source manifests |
| WorldPartitionBuilder | `2.1.0-05C` | Reference meshes и static-batch subsets |
| DonorWorldBaselineBuilder | `1.0.0-06B1` | Whitelist sanitation и canonical scene |
| DonorWorldCellizationBuilder | `1.1.0-06B2-v5.1` | Ownership, additive scenes и collision |
| DonorWorld material/texture pipeline | `06B2-v5.1.5` | Temporary HDRP compatibility presentation |

Изменение версии любого инструмента считается изменением входного контракта и
требует diff report. Тихая замена инструмента при сохранении старого baseline ID
запрещена.

## 4. Когда регенерация разрешена

Допустимые причины:

1. Локальный generated payload отсутствует после clone/clean.
2. Unity удалил или переимпортировал ignored generated assets.
3. Проверяется воспроизводимость неизменной baseline revision.
4. Одобрено исправление importer/sanitizer/cellization defect.
5. Одобрена новая canonical source revision.
6. Требуется намеренно сформировать новую baseline revision после принятого
   diff.

Недопустимые причины:

- желание «освежить» карту из текущей установленной donor-игры;
- замена отсутствующего frozen файла похожим файлом;
- ручное редактирование generated scenes, meshes, materials или textures;
- попытка устранить remaster debt внутри temporary baseline;
- смена origin, scale, cell assignment или stable IDs без отдельного решения и
  миграции.

## 5. Предусловия

До первой записи в generated boundary необходимо:

1. Полностью прочитать `AGENTS.md` и актуальный milestone prompt.
2. Проверить Git status и отделить unrelated user changes.
3. Разрешить staging roots только через ignored local configuration.
4. Убедиться, что donor installation используется только read-only.
5. Проверить точный SHA-256 frozen `GAME.unity` и `path_id_map.json`.
6. Проверить полный source-set fingerprint и все 12 normalized manifests.
7. Проверить project-owned frozen inputs, sanitation policy и tool versions.
8. Остановиться до записи при любом отсутствующем или несовпадающем входе.
9. Убедиться, что destination находится только под
   `Assets/Game/LegacyImport/RuntimeBaseline/`.
10. Зафиксировать исходный baseline revision ID, commit ID и fingerprints для
    последующего сравнения.

Hash mismatch является hard failure. Fallback на текущую donor-установку
запрещён.

## 6. Порядок регенерации

### 6.1 Dry run

Запустить project-owned dry-run из:

`Tools/MSC Remake/World Baseline 06B2/`

Dry run обязан подтвердить:

- один и тот же source revision;
- тот же набор 3 842 stable entities;
- тот же ownership plan;
- тот же scene set: одна global и 49 cell scenes;
- тот же collider allowlist и gameplay-anchor set;
- отсутствие записи за пределами generated boundary.

### 6.2 Canonical sanitation

Canonical 06B1 scene воспроизводится только из hash-locked source set и
sanitation policy `06B1.4`.

Проверяются:

- semantic fingerprint;
- runtime mesh/material payload fingerprint;
- whitelist components;
- отсутствие donor scripts, FSM и old Unity runtime dependencies;
- identity, transforms, activation state и replacement metadata.

### 6.3 Cellization и presentation

Cellization обязана:

- сохранить исходные coordinates, scale и orientation;
- не разрезать large/continuous geometry без seam-safe инструмента;
- сохранить deterministic source-cell assignment;
- сохранить global ownership для непрерывной geometry и разрешённых global
  colliders;
- пересоздать ровно 32 allowlisted static colliders;
- пересоздать 15 project-owned anchors;
- использовать shared materials/textures без runtime material instances;
- сохранить `LegacyTextured` как active temporary presentation mode;
- оставить rejected prototype visuals неактивными.

### 6.4 Валидация результата

После генерации обязательны:

- full source/cellization validator;
- проверка всех 50 generated scenes;
- проверка duplicate stable IDs, ownership и replacement keys;
- проверка forbidden components и donor hierarchy dependencies;
- проверка collider topology и representative raycasts;
- repeated additive load/unload;
- проверка active profile и Bootstrap wiring;
- Git boundary audit;
- reproducibility comparison с замороженной revision.

Тест или build нельзя отмечать `PASS`, если он фактически не запускался.

## 7. Обязательный diff report

Любая регенерация после freeze должна создать machine-readable diff и краткое
читаемое резюме. Рекомендуемое размещение:

`Docs/WorldBaseline/RegenerationDiffs/<from>__<to>.json`

Если автоматический exporter ещё не реализован, это является `PENDING tooling`,
а не основанием пропустить сравнение.

Минимальные поля diff:

- old/new baseline revision ID;
- old/new commit ID;
- source revision и полный source hash set;
- tool и policy versions;
- scene additions/removals;
- entity, renderer, material-slot, collider и anchor counts;
- stable ID additions/removals/duplicates;
- transform и activation changes;
- ownership/global-cell changes;
- replacement-key changes;
- collider type/mesh/trigger changes;
- material family, texture-role и fallback changes;
- source, semantic, payload, ownership и presentation fingerprints;
- active-profile/build-settings changes;
- known exceptions added/removed;
- validation/test results и evidence paths.

## 8. Интерпретация diff

| Результат | Условие | Действие |
|---|---|---|
| `Identical` | Все authoritative hashes, fingerprints, counts и semantic records совпали | Разрешено использовать тот же baseline revision |
| `SerializationOnly` | Отличается только неавторитетная Unity YAML serialization, semantic comparison совпал | Записать diff; revision может остаться прежней после review |
| `ReviewedExpectedChange` | Изменение одобрено и полностью объяснено | Создать новый baseline revision ID |
| `UnexpectedDrift` | Изменился любой authoritative record без одобрения | `BLOCKED`; payload не принимать |
| `SourceMismatch` | Не совпал frozen source/hash set | `BLOCKED`; генерацию остановить |
| `PolicyViolation` | Появился forbidden component, donor runtime dependency или write вне boundary | `BLOCKED`; удалить только подтверждённый generated output и расследовать |

При одинаковых inputs и tool versions изменение authoritative fingerprint
считается defect, а не новой нормой.

## 9. Версионирование и freeze

- Замороженная revision неизменяема.
- Исправление importer, sanitation, ownership, collision или presentation,
  меняющее semantic output, требует нового revision ID.
- Новый donor source set всегда требует нового source revision и baseline
  revision.
- Старый revision record не перезаписывается.
- Gameplay/save identity остаётся project-owned и не мигрирует на donor paths,
  names или instance IDs.
- Production replacement отключает соответствующий legacy renderer/collider по
  `ReplacementKey`, не изменяя frozen donor record.

## 10. Git и distribution safety

После регенерации:

1. `git status` не должен показывать generated payload.
2. `git check-ignore` должен подтверждать RuntimeBaseline boundary.
3. В Git могут попасть только tools, manifests, hashes, mappings, tests,
   reports и replacement metadata.
4. Raw extraction, normalized dumps, meshes, textures, materials и generated
   scenes не коммитятся.
5. Public/distributable build с donor baseline остаётся запрещённым.
6. Private build допускается только по правилам
   `PRIVATE_BUILD_CONTENT_AUDIT.md`.

## 11. Откат

Generated payload не является источником истины. При failed regeneration:

1. Не изменять canonical source или donor installation.
2. Сохранить logs и failed diff вне generated payload.
3. Удалять/пересоздавать можно только подтверждённый путь внутри ignored
   RuntimeBaseline boundary.
4. Повторно проверить resolved absolute destination перед recursive cleanup.
5. Восстановить payload повторной генерацией из последней принятой revision.
6. Не использовать ручные scene edits как способ «починить» drift.

## 12. Текущее evidence и незакрытые пункты

Уже подтверждено в 06B1/06B2:

- repeated canonical generation с неизменными fingerprints;
- stable material/texture и object-to-cell generation;
- full 06B2 validator для 50 scenes;
- generated-payload Git boundary.

На момент freeze:

- Milestone 06B3 strict validation и повторный deterministic export имеют
  статус `PASS`;
- baseline не регенерировался после freeze, поэтому отдельный regeneration diff
  report ещё не требовался и не создавался;
- full donor-profile private standalone build не заявлен как выполненный.

Standalone build сохраняет статус `PENDING`, пока не появится фактическое
evidence. Любая будущая регенерация обязана создать diff report.
