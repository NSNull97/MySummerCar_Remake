# Mesh Vegetation Painting System

## Назначение

Система предназначена для ручного production-authoring растительности на
существующей mesh-карте. Она не использует Unity Terrain, UV исходных мешей,
донорские hierarchy paths или GameObject на каждый экземпляр травы.

## Архитектура

```text
ProductionWorldStreamingManifest (49 cells)
  -> VegetationCellCatalog
       -> VegetationCellAsset per streaming cell
            -> RGBA PNG density mask in world XZ
            -> 32 x 32 m tile records
                 -> compact per-profile instance records

Scene View EditorTool
  -> nearest relevant Physics hit
       -> VegetationSurface
       -> VegetationBlocker
       -> bridge/road clearance rules
  -> affected mask rectangles only
  -> one Undo group per complete stroke
  -> dirty tiles only on MouseUp

VegetationWorldRenderer
  -> one GPU structured buffer per non-empty tile/profile batch
  -> tight per-tile bounds
  -> camera frustum + distance culling
  -> near/middle/far dithered LOD
  -> Graphics.RenderMeshIndirect
```

## Подготовка mesh-карты

1. Откройте нужную world streaming-сцену.
2. Выберите ground meshes, на которых разрешена растительность.
3. Выполните:
   `GameObject > MSC Vegetation > Mark Selection as Surface`.
4. Выберите запрещённую геометрию и назначьте подходящий blocker:
   - `Ground Road Blocker` — дорога блокирует ground только при малом
     вертикальном зазоре;
   - `Bridge Deck Blocker` — deck блокирует верхнюю поверхность, но допускает
     ground далеко под мостом;
   - `Bridge Pillar Blocker` — всегда блокирует;
   - `Building`, `Water`, `Foundation` — всегда блокируют.
5. Для дополнительных запретных объёмов добавьте
   `VegetationExclusionVolume` и выберите каналы.

Команды добавляют `MeshCollider`, только если у выбранного объекта отсутствует
Collider и доступен `MeshFilter.sharedMesh`. Геометрия мешей при этом не
переписывается.

## Создание данных

Выполните:

`Tools > MSC Remake > Vegetation > Create or Update Cell Assets`

Результат находится в:

`Assets/Game/World/Content/Vegetation`

Сборщик идемпотентен. Он сверяет catalog с текущим streaming manifest и
сохраняет нарисованные PNG-маски. Пустые маски не раздуваются в нативные
несжатые texture assets.

## Рисование

1. Выполните:
   `Tools > MSC Remake > Vegetation > Activate Mesh Painter`.
2. Выберите профиль/канал:
   - R — ShortGrass;
   - G — MeadowGrass;
   - B — TallGrass;
   - A — Decorative.
3. Настройте radius, strength, hardness и spacing.
4. Выберите Paint, Erase, Smooth или Noise.
5. ЛКМ рисует, Shift+ЛКМ стирает, Esc отменяет текущий штрих.

Raycast анализирует ближайшую релевантную геометрию. Инструмент не рисует
сквозь дорогу, здание или воду на землю под ними. Если кисть пересекает границу
ячеек, изменяются все пересечённые маски. PNG записывается один раз после
штриха; во время движения мыши читается и меняется только затронутый rect.

## Генерация instances

После MouseUp перестраиваются только dirty tiles. Candidate positions
получаются из глобальных grid coordinates и stable hash, поэтому одинаковы
после повторной сборки и не образуют независимый random pattern на границах
ячеек.

Каждый candidate проходит:

1. density probability;
2. точный downward raycast на `VegetationSurface`;
3. blocker/water rejection;
4. slope limit профиля и surface override;
5. world-height range;
6. exclusion volume.

Трава только частично ориентируется по normal поверхности. Степень задаётся
`VegetationProfile.SurfaceNormalAlignment`.

## Runtime

Добавьте
`Assets/Game/World/Content/Vegetation/VegetationWorldRuntime.prefab`
в project-owned world presentation или production-override сцену, которая
существует вместе с активной камерой.

Renderer:

- не создаёт GameObject на instance;
- хранит 24 bytes на instance в serialized tile data и GPU buffer;
- рисует каждый непустой tile/profile отдельными tight bounds;
- задаёт текущую камеру каждому indirect draw;
- не выделяет `MaterialPropertyBlock` в горячем цикле;
- освобождает buffers при disable/destroy;
- перестраивает buffers после завершения штриха.

## Debug

На runtime prefab доступен `VegetationDebugRenderer`. Флаги:

- Density Mask Overlay;
- Blocker Mask Overlay;
- Dirty Tile Bounds — последний перестроенный набор;
- Candidate Positions;
- Rejected Samples — цвет по причине;
- Instance Count Per Tile;
- LOD And Culling Bounds.

Debug samples ограничены 128 candidates и 128 rejections на tile, чтобы не
раздувать authored data.

## Проверка

Автоматически:

```text
Tools > MSC Remake > Vegetation > Validate Production Configuration
```

Ручной smoke test:

1. Добавить prefab renderer в тестовую world presentation сцену.
2. Нарисовать каждый из четырёх каналов рядом с границей двух ячеек.
3. Убедиться, что трава появляется по обе стороны без шва.
4. Провести кистью через road/building/water — под ними density не должна
   добавляться.
5. Проверить ground под высоким bridge deck и запрет около pillars.
6. Undo/Redo должен целиком откатывать/возвращать один штрих.
7. В debug включить dirty bounds: после MouseUp должны выделиться только
   затронутые tiles.
8. Пройти LOD distances в Scene/Game View и проверить dither, shadows, wind и
   motion vectors.
9. Профилировать GPU/CPU на целевой плотности до отключения старой
   Phase 1 растительности.

## Ограничения и риски

- Existing map meshes намеренно не были автоматически классифицированы:
  неверное массовое назначение blocker/surface опаснее ручной разметки.
- Runtime prefab намеренно не внедрён в Bootstrap автоматически, пока маски
  пусты и карта не размечена.
- Текущий общий материал — технический HDRP grass baseline. Для финального
  вида нужны authored atlas/mesh variants и визуальная калибровка профилей.
- Текущая ForwardOnly-подача использует дешёвое hemispheric lighting и
  поддерживает alpha-clipped shadow caster; полноценное художественное
  получение HDRP light-loop теней следует проверить при замене технического
  материала.
- `RaycastNonAlloc` ограничен 256 hits на луч. Это заведомо выше ожидаемой
  вложенности карты; validator/manual debug должны выявить аномальную
  многослойную геометрию.
