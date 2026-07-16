# Канонический источник donor-карты

Дата фиксации: 2026-07-16

Source revision: `msc-world-baseline-04a1.1-c3f2f337`

Database version: `04A1.1`

Classification: `TemporaryDirectImport`

## Выбранный источник

Каноническим источником является уже существующий frozen AssetRipper export:

```text
rootId: rawExtraction
configured relative root: raw/world/milestone-04a1
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

Абсолютный путь является только текущим local resolution из ignored config.
Machine-independent identity задаётся `rootId`, relative path и SHA-256 в
project-owned source manifest.

| Файл | Размер | SHA-256 |
|---|---:|---|
| `ExportedProject/Assets/_Scenes/GAME.unity` | 234 261 975 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| `AuxiliaryFiles/path_id_map.json` | 757 430 | `dfdbd71aac8a942c78f27dfd54d73ff3f1fdce3670271482a6fd2c6aa8c73165` |

Оба hash повторно проверены read-only 2026-07-16.

## Почему этот revision канонический

1. Он использовался для 04A1 normalization и project-owned stable IDs.
2. Он использовался для M05C static-batch subset reconstruction.
3. Его полная карта была визуально проверена пользователем и принята как
   хороший geometry/layout baseline 2026-07-15.
4. Все durable project tables и landmark/collider records ссылаются именно на
   этот revision.
5. Текущая переустановленная donor-копия имеет drift двух shared-assets
   containers и не может быть смешана с frozen export.

Повторное извлечение из текущей установки создало бы второй несовместимый
revision и нарушило бы stable identity/provenance.

## Исторические source containers

Frozen export был получен из следующего набора:

| Donor relative path | Historical SHA-256 | Текущее состояние |
|---|---|---|
| `mysummercar_Data/level2` | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` | Текущая установка всё ещё совпадает |
| `mysummercar_Data/sharedassets3.assets` | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` | Historical only; текущая установка отличается |
| `mysummercar_Data/sharedassets3.resource` | `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b` | Historical only; текущая установка отличается |

Read-only hashes текущей установки 2026-07-16:

| Donor relative path | Current SHA-256 |
|---|---|
| `mysummercar_Data/level2` | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` |
| `mysummercar_Data/sharedassets3.assets` | `9511802c7fbcc5abcb11cb800d8fbba69edc38fea45cdde3edd2476e855331df` |
| `mysummercar_Data/sharedassets3.resource` | `53aa0a2198b29ffe24d33a6d5e38a219d5724c99f5a737b537889865f1ea6fb1` |

Правило: frozen `GAME.unity`, frozen path map, normalized manifests и
project-owned 04A1.1 inputs образуют один неделимый source set. Подмена
отдельного mesh/resource файла из текущей donor-копии запрещена.

## Normalized source lock

Configured relative root:

`normalized/world/milestone-04a1`

Observed local resolution:

`E:\GAYmDev_Studio\MySummerCar_Remake_Game_DonorStaging\normalized\world\milestone-04a1`

| Relative path | SHA-256 |
|---|---|
| `WorldColliderManifest.csv` | `5de8f612ff4dd6a23d9e18e23ecbba88e520a1334533dfee57a2bb14e839cd79` |
| `WorldGeometryManifest.json` | `6b3161dd4d5e3ed37ea35d57c1ab4fd6a8c653352eac62640cf0cd7b31539bee` |
| `WorldInteriorManifest.csv` | `7c38e4ce0af0c09f2bd0d04b7b716b4b788f50c2552e3c7b6546ee1d9767aa99` |
| `WorldLandmarkManifest.csv` | `f55d88c326d1b3a981a13c51e13f426733bed14754f764757ec53e22bed30115` |
| `WorldMeshManifest.csv` | `d91370e086626381af44f5be3bffe6067deef693527c630099e970a53f395f8e` |
| `WorldMissingReferences.csv` | `33093bf25c912ee9785d1d9d5461c7ecd65d9c9d11620160650faa309b6c0e6c` |
| `WorldObjectPlacements.csv` | `b894b8a6416fc1b74eabb1821b71e407cbedbd3ac9afabeee262c7891004758a` |
| `WorldRoadManifest.json` | `db1ebce35ae0e8587506e0f79d35cc4ba45fad388b2b7e6374f055508250b6b2` |
| `WorldTerrainManifest.json` | `4b20748caf38dbb42a74aecfdc62b26be4825bd6c3adc50fea49bdc7693dc9f4` |
| `WorldUnsupportedObjects.csv` | `8a699d6e669dd4231344bde37c0f888ad048858d270146d4932697c9277916f7` |
| `WorldVegetationManifest.csv` | `279ebac82b1c9a2c27a0f1aae96936e7875ce71fff986f923b4753e8e876bdb1` |
| `WorldWaterManifest.json` | `9dbf1576f7517e224661a438fe7347be188cde947cb25e4cef1ad5d734dc0729` |

## Project-owned frozen inputs

| Project path | SHA-256 |
|---|---|
| `Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json` | `706a4303a715539ac2d45a5ab0dff487b685bfa3f336c57a10a477a45a454fa8` |
| `Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv` | `a0f45c9eb50b2f7dff90caa9d848a0a20be463ce333eae19da576f91dd31a408` |
| `Assets/Game/World/Content/WorldTransfer/M04A1_WorldColliders.csv` | `5de8f612ff4dd6a23d9e18e23ecbba88e520a1334533dfee57a2bb14e839cd79` |
| `Assets/Game/World/Content/WorldTransfer/M04A1_WorldLandmarks.csv` | `f55d88c326d1b3a981a13c51e13f426733bed14754f764757ec53e22bed30115` |
| `Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv` | `69d3d20d88315528cfa028dc0117e6ef39edd966b4358b6ee6b1fe3b84c4dd12` |
| `Config/WorldTransferRules.example.json` | `75f3d36dd48ac3f5f767becf53133368ba22348b1e45a50431274fd6c4a230b7` |

Эти файлы являются sanitation inputs, а не donor binary payload.

## Toolchain

| Tool | Version | Роль |
|---|---|---|
| AssetRipper | `1.3.14` | Read-only external Unity export |
| WorldTransferExtractor | `1.0.0` | Deterministic YAML normalization |
| WorldPartitionBuilder | `2.1.0-05C` | Existing reference meshes и static-batch subsets |
| DonorWorldBaselineBuilder | `1.0.0-06B1` | Whitelist sanitation и canonical scene generation |
| Unity | `6000.3.11f1` | Target Editor/runtime serialization |

Ни один donor executable, runtime assembly или old `UnityEngine` assembly не
участвует в сборке remake runtime.

## Coordinate contract

| Свойство | Значение |
|---|---|
| Axes | `X,Y,Z -> X,Y,Z` |
| Up axis | `Y` |
| Units | `1 source unit = 1 project metre` |
| Rotation | identity `(0,0,0,1)` |
| Scale | `(1,1,1)` |
| Translation | `(169.98,1.611,-1040.625)` |
| Canonical scene root TRS | identity |
| Origin policy | historical garage-roof anchor; do not recenter |

Преобразование позиции:

```text
projectPosition = sourcePosition + (169.98, 1.611, -1040.625)
```

Map bounds:

| Space | Min | Max |
|---|---|---|
| Source | `(-3163.1853,-414.4400,-2254.6733)` | `(3383.1853,911.7600,3638.5908)` |
| Project | `(-2993.2053,-412.8290,-3295.2983)` | `(3553.1653,913.3710,2597.9658)` |

## Source content envelope

Canonical source содержит:

- 36 045 placements;
- 13 509 geometry records;
- 3 842 reference-world eligible records;
- 5 001 collider records;
- 49 partition cells;
- 31 global records;
- terrain, roads, bridges, water, vegetation aggregates, buildings, interiors,
  props, signs и infrastructure;
- 37 unsupported serialized class IDs, сохранённых как metadata-only evidence.

Known global geometry включает `TERRAINOUT`, `LAKEBED`, `LAKE_VEGETATION`,
railroad, tree walls, vegetation aggregates, fields и другие cross-cell meshes.
Полный стабильный список хранится в machine-readable source manifest.

## Source-lock procedure

Перед любым generated-payload изменением pipeline обязан:

1. разрешить roots через ignored local config;
2. проверить exact hash `GAME.unity` и `path_id_map.json`;
3. проверить все 12 normalized files;
4. проверить шесть project-owned frozen inputs;
5. остановиться до записи, если отсутствует или отличается хотя бы один файл;
6. не подменять отсутствующие файлы данными текущей donor-установки;
7. при том же `sourceRevisionId` и sanitation policy сравнивать semantic и
   generated-payload fingerprints с предыдущим manifest;
8. создавать payload только под ignored
   `Assets/Game/LegacyImport/RuntimeBaseline/`.

Hash mismatch — hard failure, а не предупреждение.

## Git и distribution boundary

Разрешено версионировать:

- source manifest и hashes;
- project-owned import/sanitation tools;
- reports, mappings и replacement metadata;
- tests.

Запрещено коммитить:

- frozen AssetRipper export;
- normalized external dumps;
- raw meshes/textures/materials;
- generated runtime baseline scene, meshes и materials;
- donor assemblies и decompiled source.

Baseline разрешён только в явно private local feature-parity build. Он не
`ProductionReady` и не допускается в public/distributable build без
соответствующих прав.

## Известные ограничения source revision

- Historical donor installation была mod-contaminated; revision фиксирует
  observed serialized state, но не заявляется как clean stock.
- Один unrelated Texture2D не прочитан AssetRipper.
- 2 007 records имеют bounds-review metadata.
- Runtime-instantiated gameplay, NPC, traffic и PlayMaker behavior не являются
  частью geometric source.
- Historical M05C user acceptance подтверждает узнаваемость извлечённой карты,
  но свежая ручная проверка sanitized 06B1 scene должна фиксироваться отдельно.
