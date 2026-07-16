# Разделение gameplay data и legacy visual baseline

Дата: 2026-07-16

Milestone: `06B2_DONOR_MAP_STREAMING_CELLIZATION_AND_ACTIVE_PROFILE`

Результат автоматической проверки: **PASS**

## 1. Ownership contract

| Данные | Authority | Runtime lifetime |
|---|---|---|
| Legacy renderers/colliders | Generated donor global/cell scenes | Загружаются и выгружаются streaming service |
| Legacy provenance/replacement metadata | Project-owned runtime metadata components | Совпадает с lifetime visual scene |
| Gameplay anchors | `WorldGameplayCellCatalog` | Независимы от наличия visual override |
| Persistent identity | `StableEntityId` | Не зависит от loaded Unity scene |
| Future production art | Existing production override/replacement registry | Отключает matching legacy presentation по key |

## 2. Legacy visual identity

3 842 legacy records имеют project-owned `LegacyWorldObjectId`.

Каждый record получает unique replacement key:

`legacy-world:<stable-id>`

Source hierarchy path и donor object ID сохранены только для provenance и
диагностики. Они не являются:

- save key;
- gameplay lookup;
- scene load dependency;
- interaction dispatch key;
- production override identity.

`DonorWorldLegacyReplacementRegistry` управляет presentation state через
replacement key, а не через имя GameObject.

## 3. Gameplay catalog

Project-owned catalog:

`Assets/Game/World/Content/Streaming/WorldGameplayCellCatalog.asset`

Source manifest:

`Assets/Game/LegacyImport/Manifests/WorldBaseline06B2GameplayAnchors.csv`

Catalog содержит 15 anchors с unique `AnchorId` и `StableEntityId`. Builder и
validator проверяют:

- точное количество;
- отсутствие duplicate IDs;
- finite transforms;
- соответствие position выбранной 512 m cell;
- отсутствие donor scene object references;
- сохранение anchors при переключении legacy presentation override.

Catalog прикреплён к active manifest, но не создаётся заново при каждой
cell-load операции. Поэтому порядок загрузки visuals не дублирует gameplay
objects.

## 4. Load/unload и replacement behavior

Streaming service:

- владеет только scenes, которые загрузил сам;
- учитывает scene handle, а не только path;
- корректно переопределяет ownership после внешнего unload/reload;
- не выгружает scene, которую другой owner перезагрузил независимо;
- держит global scene загруженной при смене focus cells;
- сохраняет replacement override state между unload/reload;
- поддерживает visual absence без удаления gameplay catalog.

Prototype и donor profiles имеют разные manifests. Поэтому технический
prototype fixture не становится вторым visual owner активной donor cell.

## 5. Validation evidence

EditMode подтверждает deterministic plan, manifests, profile separation и
forbidden-dependency scans.

PlayMode подтверждает:

- unique legacy IDs/replacement keys;
- gameplay catalog availability;
- global lifetime;
- repeated far-cell unload/reload;
- external unload/reload reconciliation;
- override persistence;
- prototype visuals inactive в donor profile;
- out-of-bounds recovery без donor hierarchy lookup.

Сканирование не обнаружило архитектурной зависимости gameplay/save от donor
names или hierarchy paths.

## 6. Ограничения

- 06B2 создаёт только anchor catalog, а не NPC, AI, navigation, triggers или
  полный gameplay content.
- Legacy visual metadata остаётся `TemporaryDirectImport`.
- Production replacement art ещё не создано; проверяется только способность
  отключить matching legacy presentation через стабильный key.
