# Формат world-transfer данных

## Канонические слои

1. Внешний raw слой — AssetRipper export; donor-derived payload, вне Git.
2. Внешний normalized слой — полные CSV/JSON manifests, вне Git.
3. Project-owned metadata — компактный JSON summary и CSV-таблицы без mesh/texture payload.
4. Generated ReferenceOnly — локальные Unity scenes/materials, ignored и удаляемые.

## Версии

- database schema: `1`;
- database version: `04A1.1`;
- extractor: `msc-world-transfer-extractor/1.0.0`;
- scene generator: `1.0.0`;
- classification rules: schema `2`.

`WorldGeometryDatabaseMigration` принимает schema 0 и переводит в schema 1; неизвестные будущие версии отклоняются.

## Project-owned таблицы

- `M04A1_WorldGeometryDatabase.json` — sources/hashes, conversion, partition, counts, bounds, category/cell summaries, landmarks и limitations.
- `M04A1_WorldEntities.csv` — 13 509 геометрических сущностей, стабильные ID, transforms, bounds, hierarchy/provenance, semantic/replacement/transfer status и eligibility.
- `M04A1_WorldColliders.csv` — 5 001 collider component.
- `M04A1_WorldLandmarks.csv` — 29 spatial landmark representatives.

Полная non-geometry hierarchy не дублируется в runtime CSV: она остаётся во внешнем `WorldObjectPlacements.csv` на 36 045 строк. Поэтому parent ID геометрической записи может ссылаться на отсканированный non-geometry parent; валидатор агрегирует это в одно ожидаемое предупреждение.

Terrain, road, water, vegetation, interior, landmark и collider представления не размножаются в независимые редактируемые базы: они являются детерминированными typed views/manifest subsets одной канонической entity/collider базы. Это предотвращает расхождение stable ID, transforms и provenance. Runtime schema всё же содержит соответствующие `World*Record` types для будущей загрузки и миграции.

## Stable ID

Каноническая строка:

```text
lower(sourceId|sceneName|sourceObjectId|transformId|hierarchyPath|role)
```

От SHA-256 берутся первые 16 байт и записываются как 32 lowercase hex символа. ID не зависит от Unity instance ID, порядка генерации, converted transform или случайного GUID.

## Partition

Cell size — 512 м. Обычная сущность попадает в centroid cell `cell_X_Z`; объект шире 409,6 м по X/Z или `Terrain` — в `global`. Parent/collider provenance сохраняется как metadata даже при cross-cell связи.

## Воспроизводимость

Extractor при одинаковых source hashes и rules выдаёт одинаковые project-owned stable IDs, records и cell plan. Unity generated scenes логически воспроизводимы по count/ID/stamp, но не byte-identical: Unity переиздаёт внутренние YAML fileID и `.meta` GUID после clear/rebuild. Эти локальные файлы не являются source of truth и не коммитятся.
