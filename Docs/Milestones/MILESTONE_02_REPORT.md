# Отчёт Milestone 02 — Controlled Donor Reference Pipeline

Дата: 2026-07-13  
Результат: **пройден**. Контролируемый donor-reference pipeline реализован, проверен на двух реальных мешах и остаётся независимым от donor runtime. Unity donor validation проходит без предупреждений; EditMode suite проходит `30/30`.

## 1. Что проверено

- Прочитаны `AGENTS.md`, отчёты Milestone 0–1, `Docs/PORTING_GUIDE.md`, `Docs/ART_GUIDE.md` и prompt Milestone 2.
- Подтверждены Unity `6000.3.11f1`, HDRP `17.3.0`, Input System и существующие assembly boundaries.
- Донор `D:\SteamLibrary\steamapps\common\My Summer Car` использовался только на чтение.
- Подтверждено, что установка не является clean-stock baseline; старое `Unity_Assets_Files` не использовалось и остаётся `Rejected`.
- Все raw/normalized outputs и логи размещены во внешнем staging.
- Пользователь явно разрешил установку и использование внешних инструментов.

## 2. Инструменты и provenance

- Из официального GitHub-релиза установлен AssetRipper `1.3.14` Windows x64.
- Архив проверен по опубликованному SHA-256: `808cddf66dd0357ad6b36b97de3a2aef5e3552e63af3ee0610f9a03a0378101c`.
- Установка находится вне Git: `E:\GAYmDev_Studio\Tools\AssetRipper\1.3.14`.
- Путь записан только в ignored `Config/DonorPaths.local.json`.
- Добавлен воспроизводимый конвертер `msc-glb-to-obj 1.0.0` с pinned `trimesh 4.9.0`.
- Sidecar каждого normalized OBJ содержит входной/выходной SHA-256, bounds, vertex/face counts и версии инструментов.

AssetRipper распознал donor как Unity `5.0.0f4` Mono. При общем чтении он сообщил одну ошибку несвязанного `Texture2D` в `sharedassets3.assets`; выбранные Mesh-объекты были экспортированы, захешированы и успешно импортированы. Полный asset inventory на основании этого запуска не заявляется.

## 3. Контролируемая выборка

### Environment reference

- Объект: `garage_shed_roof`.
- Mesh PathID: `2186`; level2 GameObject PathID: `1064`.
- Контейнер: `mysummercar_Data/sharedassets3.assets`.
- Container SHA-256: `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`.
- Normalized OBJ SHA-256: `ea1c43d48a1b65ccee1bb616626e82cb89b2a79e12ba9ce1e2dbe7f96f1814ed`.
- Reference path: `Assets/Game/LegacyImport/ReferenceOnly/ControlledProof/Environment/garage_shed_roof_reference.obj`.
- Replacement: `Assets/Game/World/Content/Proof/ReauthoredGarageShedRoof.prefab`.

### Vehicle-part reference

- Объект: `drum_brake_rear`.
- Mesh PathID: `125`; level3 GameObject PathID: `123`.
- Контейнер: `mysummercar_Data/sharedassets1.assets`.
- Container SHA-256: `8f0a0984f4e55229ecaebb57ef931b052f56998b9569a32e013780aa9dc78e02`.
- Normalized OBJ SHA-256: `9b558df4ea4f2f78c5c3613e1b508d37aec56d10daf2916c33fa16241b873108`.
- Reference path: `Assets/Game/LegacyImport/ReferenceOnly/ControlledProof/Vehicle/drum_brake_rear_reference.obj`.
- Replacement: `Assets/Game/Vehicle/Content/Proof/ReauthoredRearBrakeDrum.prefab`.

Original materials и textures не переносились.

## 4. Реализованный pipeline

- `DonorAssetManifest`, `DonorAssetRecord`, `DonorAssetRegistry`, `LegacyAssetReference`.
- Отдельные SHA-256 исходного контейнера и staged-файла.
- Версии schema, pipeline, importer и converter.
- Нормализация путей и containment donor/staging/project roots.
- Read-only plan с действиями `Copy`, `UpToDate`, `Conflict`, `Blocked`.
- Отдельное подтверждаемое execution; существующие отличающиеся файлы не перезаписываются.
- Повторная проверка staged/destination SHA-256.
- Idempotent registry и CSV ledger upsert.
- Интерактивные Editor-команды и отдельные batch entry points.
- Проверка production prefab/build scene dependencies и `DonorReferenceBuildGuard`.

## 5. Independent replacement proof

Project-authored builder создал:

- два независимых replacement prefab;
- четыре project-owned mesh assets — LOD0/LOD1 для каждого объекта;
- сохранённый local pivot `Vector3.zero`;
- project-owned mount point `HubAxis` на donor-local origin;
- `BoxCollider` collision proxy на каждом prefab;
- `LODGroup` с двумя уровнями;
- новый `HDRP/Lit` material `ControlledProofPaint`;
- новые BaseColor, Normal и HDRP Mask texture maps;
- два `ReauthoredAssetProvenance` asset;
- отдельную scene `Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity`.

Production prefabs не содержат `LegacyAssetReference` и не зависят от `ReferenceOnly` или `Imported/DonorGenerated`. Удаление reference-only каталога не нарушает их dependency graph.

Replacement geometry и material set являются упрощённым техническим proof, а не финальным production-art; это явно зафиксировано в provenance и ledger.

## 6. Основные созданные и изменённые файлы

- `Assets/Game/LegacyImport/Runtime/*.cs` — manifest/registry/provenance contracts.
- `Assets/Game/LegacyImport/Editor/Pipeline/*.cs` — path policy, SHA, planner, executor, menu и batch commands.
- `Assets/Game/LegacyImport/Editor/Ledger/*.cs` — атомарный idempotent CSV upsert.
- `Assets/Game/LegacyImport/Editor/Validation/*.cs` — project validation и build guard.
- `Assets/Game/LegacyImport/Editor/Proof/*.cs` — proof metadata, builder, validator и comparison scene.
- `Assets/Game/LegacyImport/Manifests/DonorAssetManifest.controlled-proof.json` и registry/proof assets.
- `Assets/Game/World/Content/Proof/` — reauthored roof prefab и LOD meshes.
- `Assets/Game/Vehicle/Content/Proof/` — reauthored brake-drum prefab и LOD meshes.
- `Assets/Game/Presentation/Materials/Proof/` — HDRP material и три authored textures.
- `Assets/Game/Tests/EditMode/LegacyImport/*.cs` — тесты M2.
- `Tools/DonorPipeline/` — pinned GLB-to-OBJ converter и инструкция.
- Обновлены donor audit, pipeline guide, porting matrix, system map, ledger, roadmap, architecture и testing docs.

Reference OBJ и comparison scene находятся под ignored `ReferenceOnly`; raw donor payload не отслеживается Git.

## 7. Выполненные команды и результаты

1. AssetRipper headless read-only load donor — завершён; Unity `5.0.0f4` распознан.
2. Экспорт ровно двух Mesh GLB и JSON/transform metadata во внешний `raw/controlled-proof`.
3. `ConvertGlbToObj.py` для двух объектов — exit code `0`; normalized hashes записаны.
4. Unity `DonorImportBatchCommands.PlanBatch` — две операции `Copy`, 0 errors.
5. Unity `DonorImportBatchCommands.ExecuteBatch` — `copied=2`, exit code `0`.
6. Повторный plan — две операции `UpToDate`, 0 errors.
7. Первый proof-builder run обнаружил batch-only дефект восстановления пустого scene setup; дефект исправлен.
8. Повторный proof-builder run — exit code `0`, 6 ledger rows added.
9. Idempotency proof-builder run — `0 added, 0 updated`, exit code `0`.
10. `DonorPipelineValidationRunner.RunBatch` — passed, `0` warnings.
11. Unity EditMode Test Runner — **30 total, 30 passed, 0 failed, 0 skipped**; 18 expanded cases относятся к M2.
12. `FoundationValidationRunner.RunBatch` — passed.
13. Логи проверены на `error CS`, `warning CS`, compilation failure и unhandled exceptions в финальных runs — совпадений нет.

Игнорируемые результаты находятся под `Logs/Milestone02_*` и во внешнем donor staging.

## 8. Ledger и classifications

- Два donor meshes: `ReferenceOnly`, `ImportedReference`.
- Два replacements: `ReauthoredGeometry`, `ReplacementReady`.
- Material: `ReauthoredMaterial`, `ControlledProofReady`.
- Три textures: `ReauthoredTexture`, `ControlledProofReady`.
- Старые unresolved placeholder-строки сохранены как `Rejected`, `SupersededByReviewedSelection`.
- В ledger 40 строк данных, все classifications входят в разрешённый список.

## 9. Ручная проверка Unity

Единственная оставшаяся визуальная проверка:

1. Открыть `Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity`.
2. Убедиться, что слева расположены `REFERENCE__*`, справа — `PRODUCTION__*`.
3. Проверить pivot/mount gizmos и переключение LOD в Scene View.
4. Не добавлять comparison scene в Build Settings.

Эта визуальная проверка не заменяет и не блокирует уже пройденную автоматическую валидацию.

## 10. Ограничения и риски

1. Донорская установка модифицирована; proof привязан к её точным container hashes и не считается clean-stock эталоном.
2. AssetRipper сообщил одну ошибку чтения несвязанного `Texture2D`; полная инвентаризация не выполнялась.
3. Reauthored geometry и procedural textures предназначены только для проверки workflow и должны быть заменены полноценным art-процессом.
4. Comparison scene и donor OBJ намеренно ignored и disposable.
5. Полный Windows player build не создавался; отсутствие donor dependencies проверено Editor validator/build guard и dependency graph.

## 11. Exit gate

- [x] Manifest/record/registry/reference contracts реализованы.
- [x] Path containment, SHA-256 и importer versioning реализованы.
- [x] Planning отделён от execution.
- [x] Повторный plan возвращает только `UpToDate`.
- [x] Environment reference/replacement proof выполнен.
- [x] Vehicle-part reference/replacement proof выполнен.
- [x] Pivot и `HubAxis` mount point сохранены в пределах tolerance.
- [x] HDRP material, три authored textures, collision и два LOD созданы.
- [x] Dedicated comparison scene создана и не входит в build settings.
- [x] Production prefabs не зависят от donor/reference assets.
- [x] Donor validation проходит с 0 warnings.
- [x] EditMode suite проходит `30/30`.
- [x] Донорские binaries не отслеживаются Git.

## 12. Рекомендуемый следующий milestone

Выполнить ровно **Milestone 3 — Garage art prototype**: использовать измерения controlled proof для нового garage blockout/production shell, короткого road segment и первой целевой HDRP lighting/vegetation сцены, не расширяя donor import без отдельного scoped manifest.
