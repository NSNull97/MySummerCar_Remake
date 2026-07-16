# Политика sanitation donor world runtime baseline

Policy version: `06B1.4`

Source revision: `msc-world-baseline-04a1.1-c3f2f337`

Output classification: `TemporaryDirectImport`

## Назначение

Политика создаёт точный временный world-presentation baseline из уже
извлечённой donor-карты без переноса donor runtime logic.

Она не:

- создаёт production art;
- исправляет terrain voids, sprite forests или donor topology;
- переносит NPC, characters или gameplay;
- подключает collision/streaming active profile;
- импортирует donor lighting, weather, cameras или audio.

## Input boundary

Sanitation принимает только hash-locked:

- frozen AssetRipper `GAME.unity`;
- frozen AssetRipper `path_id_map.json`;
- 12 normalized world manifests;
- project-owned `04A1.1` database/entity/collider/landmark tables;
- project-owned `M05C_StaticBatchSubsets.csv`;
- project-owned classification rules.

Текущая donor-установка не является fallback. Любой source hash drift
останавливает generation до записи generated assets.

## Output boundary

Canonical scene:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity`

Generated scene, meshes и materials находятся только под:

`Assets/Game/LegacyImport/RuntimeBaseline/`

Payload игнорируется Git. В repository остаются только tools, source manifest,
metadata contracts, tests и reports.

## Explicit whitelist

### Source-derived data, разрешённые к восстановлению

- project-converted position, rotation и scale;
- source `activeSelf` и вычисленный effective-active state;
- static mesh data с usable audited mesh GUID;
- static-batch subset mesh, если source renderer использует donor combined
  static batching;
- static `MeshFilter` + `MeshRenderer` contract;
- semantic category для temporary material assignment;
- source stable/provenance metadata;
- source parent stable ID и source hierarchy path только как metadata.

### Project-owned runtime components

- `UnityEngine.Transform`;
- `UnityEngine.MeshFilter`;
- `UnityEngine.MeshRenderer`;
- `MSC.LegacyImport.DonorWorldBaselineSceneMetadata`;
- `MSC.LegacyImport.DonorWorldBaselineEntityMetadata`;
- одна neutral `UnityEngine.Light`;
- `HDAdditionalLightData`, только если HDRP добавляет его к project-owned
  neutral light.

Никакой другой component type в canonical scene не разрешён.

## Explicit exclusions

Всегда исключаются:

- donor `MonoBehaviour`;
- PlayMaker FSM и generated state-machine logic;
- donor/runtime assemblies и old `UnityEngine` references;
- Camera, AudioListener, AudioSource;
- donor Light, RenderSettings, sky, fog и weather;
- Rigidbody, joints, WheelCollider и gameplay triggers;
- Animation, Animator, SkinnedMeshRenderer и Cloth;
- ParticleSystem и legacy particle components;
- TextMesh и legacy UI;
- NPC/character body, clothing и accessories;
- save, platform, Steam, DRM и gameplay manager logic;
- object-name/hierarchy-path lookup как project runtime architecture.

Unknown serialized class IDs сохраняются в source audit, но не создаются как
Unity components.

## Selection algorithm

Pipeline рассматривает 3 842 `ReferenceWorldEligible` records в стабильном
порядке по project-owned stable ID.

Приоритет решений:

1. Если usable static mesh GUID отсутствует — `MetadataOnly /
   NoUsableMeshGuid`.
2. Если source record содержит `SkinnedMeshRenderer` — `MetadataOnly /
   SkinnedMeshRendererExcluded`.
3. Если source hierarchy находится под `/skeleton/` — `MetadataOnly /
   CharacterHierarchyExcluded`.
4. Если отсутствует полный `MeshFilter + MeshRenderer` static contract —
   `MetadataOnly / MissingStaticMeshRendererContract`.
5. Иначе renderer принимается, а все неприменимые source components
   отбрасываются.

В итоговом плане `06B1.4` четвёртая причина не имеет residual records: все
1 237 metadata-only records распределены между первыми тремя причинами.

## Итоговые counts

| Метрика | Count |
|---|---:|
| Eligible entities | 3 842 |
| Renderer accepted | 2 605 |
| Metadata-only | 1 237 |
| `NoUsableMeshGuid` | 1 058 |
| `SkinnedMeshRendererExcluded` | 62 |
| `CharacterHierarchyExcluded` | 117 |
| Effective active entities | 2 777 |
| Effective inactive entities | 1 065 |
| Active renderers | 2 123 |
| Inactive renderers | 482 |
| Runtime colliders | 0 |

Count drift является validation failure.

## Activation semantics

Project entity table хранит `activeSelf`, но 850 eligible records имеют
`activeSelf=true` под inactive ancestor. Поэтому pipeline вычисляет
effective-active state по полной 36 045-record hierarchy.

Canonical scene flatten-ит source hierarchy в два presentation roots:

- `SANITIZED_STATIC_RENDER_GEOMETRY`;
- `EXCLUDED_SOURCE_METADATA_ONLY`.

Flattening не меняет donor activation:

- effective-active records создаются active;
- effective-inactive records остаются inactive;
- source parent stable ID и source hierarchy path сохраняются в metadata;
- gameplay не получает права использовать hierarchy path для lookup.

## Canonical scene hierarchy

```text
DONOR_WORLD_BASELINE_TEMPORARY_DIRECT_IMPORT
  SANITIZED_STATIC_RENDER_GEOMETRY
  EXCLUDED_SOURCE_METADATA_ONLY
  PROJECT_OWNED_DEVELOPMENT_LIGHTING
    Neutral_Directional_Light
```

Root transform и три container transforms должны быть identity. Entity
GameObjects именуются по stable ID для диагностики, но это имя не является
runtime identity.

## Mesh sanitation

### Direct meshes

Direct renderer records используют audited source mesh copies в dedicated
runtime-baseline boundary.

Итоговый план:

- 922 direct renderer records;
- 446 unique direct source mesh assets;
- 460 unique whitelisted mesh GUID с учётом combined/static-batch sources.

### Static batching

1 683 renderer records используют donor combined static meshes. Для каждого
record pipeline:

1. читает project-owned `m_SubsetIndices` mapping;
2. копирует только принадлежащие record submeshes;
3. compact-ит vertex/index data;
4. восстанавливает local geometry относительно source TRS;
5. сохраняет отдельный derived mesh по stable ID.

Использование полного combined mesh для каждого renderer запрещено: оно
дублирует крупные части карты и создаёт ложные пространственные «шипы».

## Temporary materials

- shader: `HDRP/Unlit`;
- 22 semantic category materials;
- project-owned diagnostic colors;
- GPU instancing включён;
- donor textures не назначаются;
- shadows, reflection probes, light probes и motion vectors отключены.

Эти materials предназначены только для читаемого geometry/layout review и не
являются `ReauthoredMaterial` или `ProductionReady`.

## Neutral development lighting

Canonical scene не содержит donor sky, fog, post-processing, weather или
lighting logic.

Разрешены:

- flat neutral ambient;
- fog off;
- skybox null;
- reflection intensity 0;
- одна project-owned directional light без shadows.

Эта light не является будущим production weather/lighting owner.

## Collision policy

Source collider metadata: 5 001 records.

Runtime collider count в 06B1: `0`.

Не создаются:

- source triggers;
- source Rigidbody/joints;
- automatic MeshCollider из render mesh;
- физические материалы;
- traversal safety surfaces.

Collision отложена до 06B2, где потребуется отдельный whitelist:

- безопасные static non-trigger candidates;
- ownership global/cell;
- seam и duplicate checks;
- continuous road/terrain traversal;
- explicit handling donor voids и out-of-bounds risks.

Отсутствие runtime collision в 06B1 является известной границей, а не
пропущенной ошибкой импорта.

## Metadata contract

Каждая entity содержит project-owned:

- stable ID;
- replacement key;
- source object ID;
- source parent stable ID;
- source hierarchy path;
- source mesh GUID;
- source cell ID;
- semantic category;
- source component class IDs;
- sanitation disposition и reason;
- source `activeSelf`;
- source effective-active state;
- renderer-present flag;
- classification `TemporaryDirectImport`.

Future production override должен использовать stable ID/replacement key и
отключать matching legacy renderer/collider явно.

## Validation contract

Generation/validation должна завершаться failure при:

- source hash mismatch;
- source/project input отсутствует;
- entity/count drift;
- duplicate stable ID;
- unexpected root/container hierarchy или non-identity root TRS;
- missing script;
- запрещённый component type;
- donor script/FSM/runtime assembly dependency;
- unexpected mesh/material path;
- missing, empty или non-finite mesh;
- stale generated asset;
- texture dependency в temporary material;
- runtime baseline scene в обычных Build Settings;
- production/gameplay dependency на RuntimeBaseline или ReferenceOnly;
- runtime code lookup по donor hierarchy/name;
- semantic fingerprint drift при неизменном source revision и policy;
- generated mesh/material payload fingerprint drift при неизменном source
  revision и policy.

## Distribution policy

Baseline:

- разрешён в private local feature-parity development;
- не является final art;
- не допускается в public/distributable build без explicit rights;
- должен быть streamable и профилируемым;
- должен быть полностью заменяемым production override слоями.

## Known accepted visual debt

До remaster phase могут временно сохраняться:

- sprite forests и tree-wall cards;
- flat proxy fields;
- terrain voids;
- under-map water/swamp hacks;
- donor LOD/combined-mesh особенности;
- temporary category colors вместо textures.

Critical collision, traversal, streaming и out-of-bounds risks не считаются
принятым art debt и должны быть безопасно обработаны в 06B2/06B3.
