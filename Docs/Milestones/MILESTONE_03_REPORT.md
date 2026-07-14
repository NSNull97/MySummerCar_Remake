# Отчёт Milestone 03 — Garage Art Prototype

Дата: 2026-07-14

Результат: **пройден**. Создан воспроизводимый garage art prototype на Unity 6 HDRP без donor-зависимостей в production-сцене. EditMode suite проходит `39/39`; M3, foundation и donor-pipeline validators проходят; Windows x64 performance player и проверяемый 1080p capture созданы.

## 1. Что было проверено

- Прочитаны `AGENTS.md`, prompt Milestone 3, отчёт Milestone 2, `Docs/ART_GUIDE.md`, `Docs/WORLD_WEATHER.md` и donor audit.
- Проверены Unity `6000.3.11f1`, HDRP `17.3.0`, текущие asmdef-границы, Bootstrap и Build Settings.
- Повторно использованы только ранее проверенные metadata/hash/bounds объекта `garage_shed_roof`; donor installation не изменялась.
- Подтверждено, что до Milestone 3 не было production world/art реализации кроме упрощённого Milestone 2 proof.
- Исходно грязный worktree и пользовательские изменения сохранены.

## 2. Реализованный scope

- Production-сцена `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`.
- Модульный garage shell: бетонная плита, стены, rebuilt roof, две створки vehicle door, side door, окно и несущая рама.
- Интерьер/пропсы: workbench, shelves, tool cabinet, oil drum, fuel can, parts crate и tool rail.
- Rebuilt 180-метровый gravel road segment с отдельной обочиной, MeshCollider и двумя LOD.
- Rebuilt terrain patch с собственной сеткой, материалом и MeshCollider.
- 34 project-authored low-poly spruce instances с двумя LOD и trunk collision.
- Два reusable HDRP lighting preset: neutral и late-day; Physical Sky, sun в lux, fixed EV100, ACES, restrained bloom и volumetric fog.
- Библиотека из 12 HDRP/Lit материалов и 36 новых 128×128 PBR maps.
- Comparison scene с donor reference и rebuilt roof, исключённая из Build Settings и Git donor payload.
- Scale/pivot record с исходными hash, bounds, осевым mapping и tolerance 0,005 м.
- Editor builder, validator, static metrics collector, performance player builder и opt-in runtime probe.
- Девять новых EditMode test cases; всего suite `39/39`.

## 3. Измерения и provenance

Проверенный donor roof:

- объект: `garage_shed_roof`, Mesh PathID `2186`, level2 GameObject PathID `1064`;
- source container SHA-256: `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`;
- normalized OBJ SHA-256: `ea1c43d48a1b65ccee1bb616626e82cb89b2a79e12ba9ce1e2dbe7f96f1814ed`;
- donor-local bounds min: `(-2.280219, -1.648790, 0.179946)` м;
- donor-local bounds max: `(2.489786, 1.861611, 0.524957)` м;
- mapping в production y-up: `(X,Y,Z) -> (X,Z,Y)`;
- donor/reference pivot и production roof pivot: `(0,0,0)`;
- максимальная разница bounds/pivot: меньше `0,005` м.

Только roof bounds/pivot считаются валидированными donor-измерениями. Wall openings, мебель, terrain и 180-метровая кривая дороги — явно помеченная самостоятельная реконструкция, а не точная выгрузка layout.

Ledger дополнен семью idempotent rows: `ReauthoredGeometry`, `ReauthoredMaterial`, `ReauthoredTexture`, `Reimplemented` и `ReferenceOnly`. Повторный builder run сообщил `added=0`, `updated=0`.

## 4. Материалы

Созданы reusable материалы: concrete, painted wood, corrugated metal, structural metal, workbench wood, road gravel, road shoulder, soil/grass, foliage, bark, window glass и neutral comparison material.

Для каждого созданы BaseColor, tangent-space Normal и HDRP Mask maps. Packing Mask Map:

- R — Metallic;
- G — Ambient Occlusion;
- B — Detail Mask;
- A — Smoothness.

Donor textures и material assets не использовались. Наборы являются prototype baseline и должны быть заменены полноценными authoring/bake assets до статуса `ProductionReady`.

## 5. Основные созданные и изменённые файлы

### Runtime и Editor code

- `Assets/Game/World/Runtime/GaragePrototype/GaragePrototypeSceneMarker.cs`;
- `Assets/Game/World/Runtime/GaragePrototype/GaragePrototypePerformanceProbe.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypePaths.cs`;
- `Assets/Game/Editor/GaragePrototype/GarageScalePivotRecord.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypeMeshFactory.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypeAssetBuilder.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypeMetrics.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypeValidator.cs`;
- `Assets/Game/Editor/GaragePrototype/GaragePrototypePerformanceBuild.cs`;
- `Assets/Game/Tests/EditMode/GaragePrototype/GaragePrototypeContentTests.cs`.

### Generated tracked content

- `Assets/Game/World/Content/GaragePrototype/` — 11 mesh assets, 7 prefabs и production scene;
- `Assets/Game/Presentation/Materials/GaragePrototype/` — 12 materials и 36 textures;
- `Assets/Game/Presentation/Lighting/GaragePrototype/` — 2 VolumeProfile и 2 lighting prefab;
- `Assets/Game/LegacyImport/Manifests/M3_GarageScalePivotRecord.asset`;
- `ProjectSettings/EditorBuildSettings.asset` — production scene добавлена после Bootstrap, существующая `Assets/OutdoorsScene.unity` сохранена;
- `ProjectSettings/ProjectSettings.asset` — runtime frame-timing setting не оставлен включённым; performance builder временно включает и восстанавливает его.

### Reference-only/ignored и локальные outputs

- `Assets/Game/LegacyImport/ReferenceOnly/Comparison/GarageM3Comparison.unity`;
- `PerformanceCaptures/Milestone03/`;
- `Builds/Milestone03/`;
- `Logs/Milestone03_*`;
- `TestResults/Milestone03_*`.

### Документация/provenance

- `Docs/Performance/MILESTONE_03_GARAGE_CAPTURE.md`;
- `Docs/Porting/PORTING_LEDGER.csv`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/Porting/PORTING_MATRIX.md`;
- `Docs/Porting/SYSTEM_MAP.md`;
- `Docs/ART_GUIDE.md`;
- `Docs/WORLD_WEATHER.md`;
- `Docs/ROADMAP.md`;
- `Docs/ARCHITECTURE.md`;
- `Docs/Architecture/CURRENT_REPOSITORY_STATE.md`;
- этот отчёт.

## 6. Выполненные команды и тесты

| Команда/проверка | Результат |
|---|---|
| `GaragePrototypeAssetBuilder.RunBatch` | exit `0`; `triangles=7904`, `renderers=142`, `materials=11`, `lodGroups=36` |
| Повторный `GaragePrototypeAssetBuilder.RunBatch` | exit `0`; ledger `added=0`, `updated=0` |
| `GaragePrototypeValidator.RunBatch` | exit `0`; `M3_GARAGE_VALIDATION_OK` |
| Unity EditMode Test Runner | `39 total`, `39 passed`, `0 failed`, `0 skipped` |
| `FoundationValidationRunner.RunBatch` | exit `0` |
| `DonorPipelineValidationRunner.RunBatch` | exit `0` |
| Windows x64 Development Player build | успешно; итоговый каталог 217 388 044 байта |
| 1080p performance capture | 180 warmup + 600 samples; JSON и контрольный PNG созданы |

Финальные логи проверены на `error CS`, compilation failure и необработанные исключения. Unity периодически сообщал `Curl error 35` при попытке доступа к Unity cloud endpoint; сборка, валидация и тесты от этого не зависели. `UnityConnectSettings` после build был возвращён к исходному выключенному состоянию.

## 7. Performance result

Полная методика и hardware находятся в `Docs/Performance/MILESTONE_03_GARAGE_CAPTURE.md`.

- static: 7 904 triangles, 142 renderers, 70 colliders, 36 LODGroup, 1 light;
- player CPU-side mean: 3,249 мс;
- p95: 7,458 мс;
- p99: 9,474 мс;
- worst: 14,537 мс;
- provisional 60 FPS CPU budget: 16,667 мс — пройден;
- GPU FrameTiming: недоступен, отдельный GPU/present pass не заявляется.

## 8. Ручные шаги Unity

Автоматическая exit-gate validation завершена. Для обязательного art review:

1. Открыть `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`.
2. В Scene/Game View проверить garage silhouette, door openings, road shoulder, terrain seams, LOD transitions и collider gizmos.
3. Для neutral review заменить instance `LateDayPreset_Active` prefab-ом `Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_Neutral.prefab`; исходный late-day prefab не удалять.
4. Открыть `Assets/Game/LegacyImport/ReferenceOnly/Comparison/GarageM3Comparison.unity` и проверить native-axis reference, mapped y-up production roof и оба zero-pivot marker.
5. Не добавлять comparison scene в Build Settings.

## 9. Ограничения и риски

1. Полный garage layout не измерен в donor: валидированы только roof bounds и zero pivot.
2. Геометрия и 128×128 procedural textures имеют статус `PrototypeReady`, а не `ProductionReady`.
3. Road curve/height не основаны на извлечённом donor centerline; spline authoring, ditches, puddles, wetness и decals отложены.
4. Растительность — простые cone/cylinder meshes без wind, billboard/impostor и species variation.
5. Внутреннее освещение, exposure adaptation между интерьером/улицей и baked GI не настроены.
6. GPU time не был доступен через D3D12 FrameTiming; текущая performance оценка ограничена CPU-side synchronous render request.
7. `MSC.World.Runtime` временно ссылается на Render Pipelines Core только ради opt-in prototype performance probe; перед production world architecture probe следует перенести в отдельную development assembly или удалить.
8. Debug fly camera, player и vehicle системы намеренно не реализованы.

## 10. Exit gate

- [x] Production garage prototype scene создана.
- [x] Comparison scene создана и исключена из build.
- [x] Garage roof scale/pivot сохранены в пределах 0,005 м.
- [x] Doors, workbench, shelves и prop set rebuilt.
- [x] 180 м road, terrain blending baseline и vegetation созданы.
- [x] Neutral/late-day HDRP presets созданы; exposure физически согласован с lux.
- [x] Reusable material library с новыми PBR maps создана.
- [x] LOD/collision добавлены и валидируются.
- [x] Production scene не зависит от `ReferenceOnly` или `DonorGenerated`.
- [x] Static и player performance capture выполнены; предварительный CPU budget пройден.
- [x] EditMode suite проходит `39/39`.

## 11. Рекомендуемый следующий milestone

Выполнить ровно **Milestone 4 — Player and interaction**: movement/look/crouch, capability-based interaction query, pickup/carry/place/drop и минимальный tool interaction, не расширяя world/weather или vehicle scope.
