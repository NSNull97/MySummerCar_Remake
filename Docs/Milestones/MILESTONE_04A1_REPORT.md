# MILESTONE 04A1 — Full World Geometry Transfer Report

Дата завершения: 2026-07-14

Режим: `ReferenceGeometryFirst`

Database: schema `1`, version `04A1.1`
Статус: автоматизированный data/layout/proxy baseline завершён; ручная визуальная fidelity-проверка открыта.

## 1. Executive summary

Полная обнаруженная serialized-сцена `GAME` извлечена из локальной лицензированной donor-установки строго read-only во внешний staging. Созданы полный placement inventory, versioned project-owned world database, детерминированные stable IDs, semantic/context classification, 512-метровая partition на 49 cells, ignored ReferenceOnly proxy world, Editor tooling, runtime streaming boundary, validation и tests.

Player и Interaction не изменялись. К 04B или Milestone 05 переход не выполнялся. Donor geometry/textures/materials не стали production assets и не попали в Git.

## 2. Repository and path validation

- Repository: `E:\GAYmDev_Studio\MySummerCar_Remake`.
- Unity: `6000.3.11f1`.
- HDRP: `17.3.0`.
- Input System: `1.19.0`.
- Donor: `D:\SteamLibrary\steamapps\common\My Summer Car` — существует, проверен read-only.
- External staging: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging`.
- Legacy reference: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_LegacyReference`.
- Machine paths находятся только в ignored local config/локальных командах; runtime C# абсолютных путей не содержит.

Donor-установка mod-contaminated; результаты привязаны к exact hashes и не объявлены stock baseline.

## 3. Donor world sources

| Файл | Размер | SHA-256 | Роль |
|---|---:|---|---|
| `mysummercar_Data/level2` | 104 297 292 | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` | Serialized `GAME` scene |
| `mysummercar_Data/sharedassets3.assets` | 533 373 096 | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` | Mesh/assets references |
| `mysummercar_Data/sharedassets3.resource` | 625 805 147 | `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b` | Resource stream |
| extracted `GAME.unity` | 234 261 975 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` | External normalization input |

AssetRipper export: 17 247 files, 1 754 855 811 bytes, 5 scenes, 1 939 mesh-like assets, 986 materials и 1 320 textures. Полный inventory: `Docs/WorldTransfer/DONOR_WORLD_SOURCES.csv`.

## 4. Coordinate and scale findings

Donor и remake используют Y-up, identity axes/rotation/scale и `1 unit = 1 m`. Преобразование:

```text
remakePosition = donorPosition - (-169.98, -1.611, 1040.625)
```

Garage roof fixture `fb0f962be1b325cc19296c66751818c0` переводится из `(-169.98,-1.6110001,1040.625)` в `(0,~-1.19e-7,0)`. Parent-child composition, directions, rotations, scales и round trip покрыты EditMode tests.

Converted bounds: min `(-2993.2053,-412.8290,-3295.2983)`, max `(3553.1653,913.3710,2597.9658)`. Extreme Y требует ручного review.

## 5. World-data format

Durable source of truth:

- `M04A1_WorldGeometryDatabase.json` — sources, hashes, conversion, partition, counts, categories, cells, landmarks, limitations;
- `M04A1_WorldEntities.csv` — все 13 509 geometry entities;
- `M04A1_WorldColliders.csv` — 5 001 collider records;
- `M04A1_WorldLandmarks.csv` — 29 spatial representatives.

Полная 36 045-record hierarchy остаётся во внешнем `WorldObjectPlacements.csv`. Typed terrain/road/water/vegetation/interior manifests — детерминированные views канонической базы, а не расходящиеся hand-edited sources.

## 6. Stable-ID strategy

Каноническая строка — lowercase `sourceId|sceneName|sourceObjectId|transformId|hierarchyPath|role`; stable ID — первые 16 байт SHA-256 как 32 lowercase hex символа. Unity instance IDs, position, current generation order и random GUID не используются. Все 13 509 IDs уникальны; known garage fixture совпадает с extractor output.

## 7. Extraction tools

- AssetRipper `1.3.14` — external read-only Unity-project export.
- Project-owned `.NET 8` `WorldTransferExtractor 1.0.0` — streaming AssetRipper YAML parser без external NuGet dependencies.
- Unity `WorldPartitionBuilder 1.0.0` — category proxy materials, cells, global/persistent/bootstrap scenes.
- Unity World Transfer window/menu — dry run, selected/all generation, validation, missing/unsupported reports, overview, clear/rebuild.

Long full generation показывает cancelable progress в interactive Editor; отменённый partial output не проходит validation и требует rebuild.

## 8. Extraction results

- Scene GameObjects/Transforms: 36 045 / 36 045.
- Geometry entities: 13 509.
- Context-filtered reference-world entities: 3 842.
- Classified non-world geometry retained in database: 9 667.
- Unique referenced mesh GUID: 1 362.
- Entities with resolved mesh references: 6 248.
- Collider components: 5 001.
- Bounds review records: 2 007.
- Unsupported serialized class IDs: 37.
- Cells: 49; global entities: 31.
- Landmark representatives: 29.

Final extractor run: code 0, 3.295 s. Release build: 0 warnings, 0 errors.

## 9. Counts by geometry category

Reference layer: BuildingExterior 1 568; ColliderOnly 772; InteractivePropCandidate 395; StaticProp 265; BuildingInterior 211; RoadSign 163; Roof 124; Door 120; Window 67; Road 29; Floor 23; Fence 18; VegetationTree 16; SpawnMarker 14; UtilityPole 14; Water 12; Landmark 9; VegetationBush 7; Wire 4; VegetationGrass 3; Rock 3; Bridge 2; Field 2; Terrain 1. Сумма — 3 842.

Rules schema 2 устранила конкретные false positives (`shoulder` bone, mini-game terrain/ditch, store seat/water) через hierarchy context и exclusions. Исключённые records не удалены.

## 10. Terrain status

Одна `Terrain` и две `Field` records представлены как layout/provenance. Большой terrain routed to global. Heightfield, holes, splats, production TerrainData, terrain/road blending и elevation parity не заявлены.

## 11. Road-network status

Представлены 29 `Road`, 2 `Bridge`, 163 `RoadSign`, 14 `UtilityPole` и 4 `Wire` records. Centerline graph, width/elevation samples, junction topology, shoulders/ditches и driveable production surface не восстановлены. Статус: `RepresentedNeedsManualTopologyReview`.

## 12. Buildings and interiors

Представлены 1 568 exteriors, 211 interiors, 124 roofs, 23 floors, 120 doors и 67 windows. CABIN/YARD/STORE/PERAJARVI/REPAIRSHOP/INSPECTION/COTTAGE и другие location roots находятся в cell database. Door pivots сохранены как donor transforms, но floor continuity, portals, doorway clearance и visual alignment требуют ручного review.

## 13. Vegetation and props

Data records: 16 trees, 7 bushes, 3 grass, 3 rocks, 265 static props и 395 interactive candidates. Persistent GameObject на каждую травинку не создаётся. Reference visualization — editor-only bounds/point proxies; production batching/species/density остаются будущей заменой.

## 14. Water status

12 context-confirmed `MAP/` water records и 5 `MajorWater` landmark representatives. `LAKEBED` routed to global. Отдельная shoreline topology не доказана; HDRP Water, shoreline spline, elevation comparison и transition volumes не выполнялись.

## 15. Collider status

Нормализованы 1 469 Sphere, 1 280 Box, 1 216 Capsule и 1 036 MeshCollider records; 3 262 non-trigger и 1 739 trigger. В reference subset 1 413 entities содержат 1 488 collider refs. Generated proxies не создают runtime collision; production collision и gameplay intent не заявлены.

## 16. Partitioning and scenes

- Cell size: 512 m.
- Loading radius: 1 cell; unloading radius: 2 cells.
- Large-object threshold: 409.6 m; policy: global scene.
- 49 cell scenes + `World_GlobalReference` + `World_Persistent` + `WorldTransfer_Bootstrap` = 52 scenes.
- 24 neutral HDRP/Unlit category materials.
- Generated tree: 154 files, 13 015 783 bytes.

Самые плотные cells: `cell_-3_0` — 1 456, `cell_0_-3` — 671, `cell_3_-1` — 429. Полная таблица: `Docs/WorldTransfer/ZONE_STATUS.csv`.

Reference loader реализует существующий `IWorldStreamingService`, но disabled в bootstrap: ignored ReferenceOnly scenes не входят в release Build Settings. Editor overview открывает выбранный additive subset.

## 17. Validation results

Final world validator: pass — `entities=13509 eligible=3842 cells=49 warnings=1`. Он проверил:

- существование и exact SHA-256 всех трёх donor sources;
- наличие 12 normalized manifests;
- database version, CSV parsing, finite transforms и unique stable IDs;
- eligibility/cell consistency и parent metadata;
- 52 generated scene files/stamps;
- exact generated stable-ID set и category parity.

Единственное ожидаемое warning: 10 054 geometry parents являются scanned non-geometry placements; полная hierarchy сохранена во внешнем placement manifest.

Foundation validator — pass. Donor pipeline validator — pass. Player/Interaction validator — pass. Новых compiler errors нет; остаются прежние warnings о пустых placeholder asmdefs.

## 18. Landmark fidelity

Garage transform автоматически проверен. Bridge, church, cottage, landfill, water, repair, town/service и inspection groups доступны по stable ID/name через registry. Представители выбираются как shallowest geometry record в 128 m spatial bucket; некоторые names (`VideoPoker`, `office_lamp`) являются navigation anchors, не canonical landmark meshes, и намеренно отмечены для semantic review.

Runtime screenshots и measured deviations, кроме garage origin/conversion, не получены. Visual parity не заявляется.

## 19. Missing references and unsupported objects

`WorldMissingReferences.csv`: 2 007 `BoundsMetadata` records — AssetRipper combined/static mesh bounds нельзя безопасно отнести к separated placement, поэтому использован placement point. Missing mesh GUID records — 0.

`WorldUnsupportedObjects.csv`: 37 class IDs `MetadataOnly`; component IDs сохранены в entity records. Одна unreadable Texture2D не блокирует geometry и не используется как final texture.

## 20. Tests executed

- Final Unity EditMode: **67/67 passed**, 0 failed, 0 skipped; test duration 12.156 s.
- Final Unity PlayMode: **9/9 passed**, 0 failed, 0 skipped; test duration 0.121 s.
- Новые WorldTransfer tests: 12 EditMode + 4 PlayMode.
- `.NET` extractor Release build: pass, 0 warnings/errors.
- `git diff --check`: pass.
- Player/Interaction diff: empty.
- Generated ReferenceOnly world tracked by Git: 0 files.

## 21. Batch commands executed

Ключевые команды:

```text
dotnet build Tools/WorldTransfer/WorldTransferExtractor/WorldTransferExtractor.csproj --configuration Release
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MSC.Editor.WorldTransfer.WorldPartitionBuilder.RebuildBatch -logFile <log>
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MSC.Editor.WorldTransfer.WorldTransferValidator.RunBatch -logFile <log>
Unity.exe -batchmode -projectPath <project> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
Unity.exe -batchmode -projectPath <project> -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>
```

Отдельно выполнены Foundation, DonorPipeline и PlayerInteraction validation runners.

## 22. Manual Unity actions still required

1. `Tools > MSC Remake > World Transfer > Open Reference Overview`.
2. Выполнить checklist из `WORLD_MANUAL_VALIDATION_CHECKLIST.md`.
3. Снять donor/remake comparison screenshots для garage, town, repair, inspection, bridge, cottage, landfill, junctions и shoreline.
4. Проверить 2 007 bounds-review records, cell seams, extreme Y, interiors, floors/doors и continuous driving topology.

Эти шаги не отмечены выполненными.

## 23. Files created and modified

Созданы:

- external `.NET` extractor under `Tools/WorldTransfer/`;
- 9 runtime C# files under World `Data/Debug/Partition/Streaming`;
- 6 Editor C# files under `Assets/Game/Editor/WorldTransfer/`;
- WorldTransfer EditMode/PlayMode tests;
- 4 project-owned database/CSV assets;
- 2 committed config examples и 2 ignored local configs;
- 20 detailed documents in `Docs/WorldTransfer/`;
- этот milestone report.

Изменены `.gitignore`, World/PlayMode asmdefs, donor provenance validator/test, PROJECT, ROADMAP и Porting audit/matrix/system map/ledger. `Assets/Game/Player` и `Assets/Game/Interaction` не изменялись.

## 24. Generated local/external paths

- Raw: `E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\raw\world\milestone-04a1`.
- Normalized: `...\normalized\world\milestone-04a1`.
- Manifest copies: `...\manifests\world\milestone-04a1` — 12 files, 25 376 501 bytes.
- Logs: `...\logs\world\milestone-04a1`.
- Unity local generated: `Assets/Game/LegacyImport/ReferenceOnly/World/Generated` — ignored.

## 25. Known risks and exact blockers

- Visual/landmark parity blocked until manual donor/remake capture review.
- Terrain heightfield, road graph/continuous surface, shoreline topology и production colliders не доказаны.
- 2 007 bounds records требуют visual/mesh-level review.
- Semantic fallback `BuildingExterior` широкая и требует refinement при replacement.
- Mod-contaminated donor baseline может отличаться от stock.
- Generated Unity YAML/meta не byte-identical после clear/rebuild; stable IDs, database plan, counts и stamps детерминированы.
- No blocker remains for regeneration, automated validation или use as a reference/layout foundation.

## 26. Exactly one recommended next prompt

Запустить **`Prompts/99_REVIEW.md`** как read-only review текущего Milestone 04A1 с отдельным вниманием к world completeness/fidelity claims. Не начинать 04B или Milestone 05 до результатов review.

## 27. Stop statement

Milestone 04A1 завершён в оговорённых границах. Коммит не создавался, потому что в текущем запросе он не требовался. Работа остановлена без перехода к 04B или 05.
