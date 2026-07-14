# Отчёт terrain transfer

## Статус

Reference-слой содержит одну контекстно подтверждённую запись `Terrain` и две записи `Field`. `TERRAINOUT` привязан к mesh GUID и переведён в `global`, но его per-object bounds заменён placement point из-за AssetRipper combined/static mesh ambiguity.

Долговечный результат — placement/provenance/cell record, а не production TerrainData. Донорская terrain geometry не импортирована в production folders, heightmap не объявлен восстановленным, holes/splats/details не перенесены.

## Покрытие и ограничения

- terrain source scene: `GAME`;
- `Terrain`: 1;
- `Field`: 2;
- большие records: `global` policy;
- world bounds представлены общим database envelope;
- elevation deviation с donor runtime не измерена;
- extreme Y и terrain seams требуют ручной проверки.

Production terrain rebuild, spline blending, vegetation terrain association и final colliders относятся к следующему world-production этапу, не к 04B/05 в этой задаче.
