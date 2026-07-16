# Disposition двух prototype world cells

Дата решения: 2026-07-16
Milestone: `06B1_EXISTING_DONOR_MAP_BASELINE_AUDIT_AND_SANITIZATION`

## 1. Авторитетное решение

Ровно две существующие custom production cells не соответствуют donor
locations:

```text
cell_0_-3
cell_0_-2
```

Для их визуального content действует полный статус:

```text
PrototypeOnly
RejectedForFidelity
InactiveInFeatureParityProfile
NotProductionReady
```

Технические проверки collision, traversal и streaming не отменяют visual
rejection. Scenes, prefabs и assets не удаляются: они сохраняются как
development fixtures и как доказательство работоспособности project-owned
streaming infrastructure.

Важное уточнение: в 06B1 выполнена классификация и зафиксирован требуемый
disposition. Фактический active-profile switch ещё не реализован. Текущий
`ProductionWorldStreamingManifest.asset` по-прежнему способен загрузить обе
prototype scenes. Их отключение именно в feature-parity runtime относится к
06B2.

## 2. Cell identity, которую необходимо сохранить

| Cell | Intended donor location | Project root stable ID | Проверенный anchor |
|---|---|---|---|
| `cell_0_-3` | Дом, гараж и двор игрока; donor context `YARD/Building/Garage` | `fc6a437b97ea997ca03a5e8bad1ba9b7` | `(153.495, 0.95, -1033.23)` |
| `cell_0_-2` | Домашний пирс, берег, озеро/дно и три hedge anchors | `7201412942822b5b4724b5e72c74065f` | `(177.67, -1.779, -894.185)` |

Сохраняются:

- cell IDs и `(X, Z)` indices;
- project-owned stable IDs;
- registry records и source-to-destination metadata;
- gameplay anchors;
- lifecycle test infrastructure;
- build/path validation contracts;
- возможность явно запустить prototype fixture profile для диагностики.

Donor hierarchy paths остаются provenance/reference metadata и не становятся
runtime identity.

## 3. Disposition runtime и scene assets

| Asset | Связь | Решение | Feature-parity состояние | Причина |
|---|---|---|---|---|
| `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity` | `cell_0_-3` | Retain | Требуется отключить в 06B2 | Technical streaming/collision fixture; visual layout rejected |
| `Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity` | `cell_0_-2` | Retain | Требуется отключить в 06B2 | Technical streaming/collision fixture; visual layout rejected |
| `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity` | `cell_0_-3` standalone playtest | Retain | Development-only, не active world | Полезен для regression старого fixture |
| `Assets/Game/World/Production/Scenes/WorldRemasterHomeShorelinePlaytest.unity` | `cell_0_-2` standalone playtest | Retain | Development-only, не active world | Полезен для collision/seam regression старого fixture |
| `Assets/Game/World/Debug/Comparison/WR_HomeYardComparison.unity` | `cell_0_-3` comparison | Retain | Inactive / excluded from normal runtime | Историческое comparison evidence; не canonical donor view |
| `Assets/Game/World/Debug/Comparison/WR_HomeShorelineComparison.unity` | `cell_0_-2` comparison | Retain | Inactive / excluded from normal runtime | Историческое comparison evidence; не canonical donor view |
| `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity` | Содержит home-yard prototype context | Retain | Только vehicle/assembly development scene | World context не считается feature-parity map и не подтверждает fidelity |
| `Assets/Game/Bootstrap/Bootstrap.unity` | Текущий installer двух cells | Retain infrastructure | Wiring должен быть profile-aware в 06B2 | Composition root и focus binding полезны; текущий visual selection устарел |
| `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset` | Ровно `cell_0_-3`/`cell_0_-2`, build `6/8` | Retain as fixture contract | Не использовать как donor feature-parity profile | Manifest доказывает lifecycle, но сейчас выбирает rejected scenes |
| `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/World_DonorBaseline_Canonical.unity` | Canonical sanitized donor baseline | Retain generated local baseline | Не активирован в 06B1 | Source для будущего feature-parity profile и deterministic cellization |

Ни одна scene в этом списке не архивирована и не удалена в 06B1.

## 4. Disposition prefab assets

Все перечисленные prefabs являются project-authored, поэтому они не являются
raw donor payload. Однако их композиция не прошла fidelity gate. Они
сохраняются как `PrototypeOnly` и не могут представлять соответствующую
локацию в feature-parity profile.

### `cell_0_-3`

| Prefab | Решение | Разрешённое использование |
|---|---|---|
| `Assets/Game/World/Production/Prefabs/WR_HomeYardPilot.prefab` | Retain / RejectedForFidelity | Root fixture для старых tests |
| `Assets/Game/World/Production/Prefabs/WR_HomeGarage.prefab` | Retain / RejectedForFidelity | Garage interaction/collision prototype |
| `Assets/Game/World/Production/Prefabs/WR_HomeHouseShell.prefab` | Retain / RejectedForFidelity | Building-shell prototype |
| `Assets/Game/World/Production/Prefabs/WR_HomeInteriorSlice.prefab` | Retain / RejectedForFidelity | Interior traversal fixture |
| `Assets/Game/World/Production/Prefabs/WR_HomeTerrainRoadDitch.prefab` | Retain / RejectedForFidelity | Terrain/road/collision fixture |
| `Assets/Game/World/Production/Prefabs/WR_PilotVegetation.prefab` | Retain / RejectedForFidelity | Vegetation/LOD prototype |
| `Assets/Game/World/Production/Prefabs/WR_SpruceTree.prefab` | Retain / RejectedForFidelity | Shared prototype vegetation element |
| `Assets/Game/World/Production/Prefabs/WR_HomePropsInfrastructure.prefab` | Retain / RejectedForFidelity | Prop/infrastructure prototype |

### `cell_0_-2`

| Prefab | Решение | Разрешённое использование |
|---|---|---|
| `Assets/Game/World/Production/Prefabs/WR_HomeShorelinePier.prefab` | Retain / RejectedForFidelity | Root fixture для старых shoreline tests |
| `Assets/Game/World/Production/Prefabs/WR_HomeShorelineWater.prefab` | Retain / RejectedForFidelity | Water/shore collision prototype |
| `Assets/Game/World/Production/Prefabs/WR_HomePier.prefab` | Retain / RejectedForFidelity | Walkable-pier fixture |
| `Assets/Game/World/Production/Prefabs/WR_HedgeSegment.prefab` | Retain / RejectedForFidelity | Hedge-anchor fixture |

Допустима будущая повторная оценка отдельного project-authored asset в рамках
remaster phase. Повторное использование не переносит автоматически статус
`Approved` на asset или cell.

## 5. Disposition mesh assets

| Mesh | Решение | Ограничение |
|---|---|---|
| `Assets/Game/World/Production/Meshes/WR_HomeYardTerrain.asset` | Retain / PrototypeOnly | Не активировать как donor-accurate terrain |
| `Assets/Game/World/Production/Meshes/WR_HomeRoad.asset` | Retain / PrototypeOnly | Не считать donor road reconstruction |
| `Assets/Game/World/Production/Meshes/WR_HomeDriveway.asset` | Retain / PrototypeOnly | Не считать donor driveway geometry |
| `Assets/Game/World/Production/Meshes/WR_DitchWater.asset` | Retain / PrototypeOnly | Не считать donor water/ditch layout |

Meshes не удаляются, потому что текущие PlayMode и validation fixtures могут
зависеть от их geometry/collision context.

## 6. Disposition material assets

Следующие материалы сохраняются как project-authored prototype presentation.
Они не дают visual fidelity и не должны перекрывать temporary donor baseline в
feature-parity profile:

| Material | Решение |
|---|---|
| `Assets/Game/World/Production/Materials/WR_Bark.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_Concrete.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_DitchWater.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_Driveway.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_Foliage.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_GravelRoad.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_InteriorWall.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_MetalRoof.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_PaintedWood.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_ReferenceOverlay.mat` | Retain / comparison-only |
| `Assets/Game/World/Production/Materials/WR_StructuralMetal.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_Terrain.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_WindowGlass.mat` | Retain / PrototypeOnly |
| `Assets/Game/World/Production/Materials/WR_WorkbenchWood.mat` | Retain / PrototypeOnly |

## 7. Disposition metadata, registries и tools

| Asset/tool | Решение | Причина |
|---|---|---|
| `Assets/Game/World/Authoring/ReplacementProfiles/WR_WorldProductionAssetRegistry.asset` | Retain | Содержит project-owned stable mappings, zone IDs и backlog |
| `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv` | Retain authoritative status | Обе cells имеют `Rejected` и `FidelityGateBlocked` |
| `Docs/WorldFidelity/WORLD_FIDELITY_STATUS_LEDGER.csv` | Retain authoritative status | Фиксирует user rejection и blocking finding IDs |
| `Assets/Game/World/Runtime/Partition/WorldPartition.cs` | Retain infrastructure | Stable cell grid и ID contract |
| `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs` | Retain infrastructure | Validated catalog contract |
| `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs` | Retain infrastructure | Additive ownership/hysteresis implementation |
| `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs` | Retain infrastructure | Explicit focus composition |
| `Assets/Game/Editor/WorldStreaming/ProductionWorldStreamingBuilder.cs` | Retain fixture builder; do not run as full-map builder | Жёстко пересоздаёт manifest ровно для двух prototype scenes |
| `Assets/Game/World/Editor/ProductionWorldCellBuilder.cs` | Retain historical prototype builder; guarded use only | Может регенерировать rejected visuals, scenes, registry и integration content |
| `Assets/Game/Editor/WorldStreaming/WorldPilotGateRemediationValidator.cs` | Retain regression validator | Проверяет полезную technical wiring |
| Existing WorldRemaster EditMode/PlayMode tests | Retain fixture profile tests | Доказывают старый lifecycle/collision, но не donor fidelity |

Registry и test fixtures нельзя удалять вместе с visual activation: gameplay
systems и будущие save/override mappings должны продолжать опираться на
project-owned stable IDs.

## 8. Что именно отклонено

Для `cell_0_-3` отклонена custom interpretation:

- road bend и driveway relationship;
- размещение и пропорциональная связь дома, гаража и площадки;
- композиция изгороди, utility pole, двора и vegetation boundary.

Для `cell_0_-2` отклонена custom interpretation домашнего берега/пирса как
соответствие оригиналу. Существующие traversal, water и hedge tests доказывают
только работоспособность prototype geometry.

Не отклонены:

- сами cell IDs;
- stable IDs и anchors;
- streaming service;
- manifest validation;
- Bootstrap focus binding;
- ownership и hysteresis;
- test harnesses;
- project-authored registry/provenance data.

## 9. Active-profile contract для 06B2

Требуемый switch должен быть явным и проектным. Минимально необходимы два
разных режима:

| Profile | Visual source | Назначение |
|---|---|---|
| Prototype fixture | Существующие `Production_cell_0_-3` и `Production_cell_0_-2` | Regression старых technical tests |
| Donor feature parity | Sanitized canonical donor baseline, позже global/cell legacy layers | Обычный private local feature-parity runtime |

Правила:

1. Donor feature-parity profile не загружает rejected custom visual roots.
2. Prototype fallback не включается silently при ошибке donor baseline.
3. Gameplay anchors и stable IDs находятся в project-owned gameplay layer, а
   не зависят от активной visual hierarchy.
4. Production override может включаться только explicit replacement key.
5. Legacy и prototype/override renderers и colliders не должны дублироваться.
6. Comparison/playtest scenes остаются development-only.
7. Private donor baseline не допускается в distributable/public profile.

Реализация switch, изменение manifest/Bootstrap и cellization запрещены рамками
06B1 и выполняются только в 06B2.

## 10. Итоговая таблица решений

| Категория | Retained | Inactive target | Archived | Deleted |
|---|---:|---:|---:|---:|
| Production cell scenes | 2 | 2 в feature-parity profile, начиная с 06B2 | 0 | 0 |
| Standalone playtest scenes | 2 | 2 вне active world | 0 | 0 |
| Comparison scenes | 2 | 2 вне normal runtime | 0 | 0 |
| Cell/world prefabs | 12 | 12 как active donor visuals | 0 | 0 |
| Prototype meshes | 4 | 4 как active donor visuals | 0 | 0 |
| Prototype materials | 14 | 14 как active donor visuals | 0 | 0 |
| Streaming/identity infrastructure | Retained | Нет | 0 | 0 |

06B1 не удаляет, не перемещает и не пересохраняет перечисленные scenes, prefabs,
meshes, materials или registry assets.
