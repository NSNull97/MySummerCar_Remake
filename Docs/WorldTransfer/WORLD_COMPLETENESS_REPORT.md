# Отчёт полноты world transfer

## Итоговые числа

| Метрика | Значение |
|---|---:|
| Discovered source GameObjects | 36 045 |
| Placement records | 36 045 |
| Geometry entities | 13 509 |
| Reference-world entities | 3 842 |
| Classified non-world geometry | 9 667 |
| Collider records | 5 001 |
| Entities с resolved mesh references | 6 248 |
| Unique referenced mesh GUID | 1 362 |
| Bounds review records | 2 007 |
| Missing mesh GUID records | 0 |
| Unsupported serialized class IDs | 37 |
| Spatial cells | 49 |
| Global entities | 31 |
| Landmark representatives | 29 |

Source scene — `GAME` из одного exact-hash donor baseline. Object/placement parity — 36 045/36 045. Geometry records не теряются при semantic exclusion.

## Coverage

- Terrain/field: represented as 3 layout records; heightfield parity pending.
- Roads: 29 roads, 2 bridges и spatial street furniture; graph/topology pending.
- Buildings/interiors: represented; 1 568 exterior и 211 interior records; portal/floor parity pending.
- Vegetation: 26 vegetation records и 3 rocks; species/density pending.
- Props: 265 static + 395 interactive candidates.
- Water: 12 map-context records; shoreline topology pending.
- Colliders: 5 001 serialized components; production collision pending.

## Partition parity

49 cell scenes содержат 3 811 entities, `World_GlobalReference` — 31. Самые плотные cells: `cell_-3_0` — 1 456, `cell_0_-3` — 671, `cell_3_-1` — 429. Полная таблица count/status — [ZONE_STATUS.csv](ZONE_STATUS.csv).

## Missing/unsupported

`WorldMissingReferences.csv` содержит 2 007 `BoundsMetadata` review records, а не отсутствующие mesh GUID: все mesh references из scene удалось сопоставить с export. `WorldUnsupportedObjects.csv` перечисляет 37 class IDs metadata-only; они не удалены из component IDs.

## Major landmarks

Автоматически представлены home/garage, town/service, repair shop, inspection, church, cottage, landfill, water и bridge groups. Garage fixture находится в origin в пределах float tolerance. Остальные требуют donor runtime screenshots/manual comparison; их наличие в базе не считается визуальной parity.

## Вывод

Milestone завершает data/layout/proxy foundation для обнаруженной serialized geometry. Он не доказывает production-ready terrain, driveable roads, final art или полную runtime-instantiated карту. Все такие пробелы находятся в manual queue, а не скрыты как «done».
