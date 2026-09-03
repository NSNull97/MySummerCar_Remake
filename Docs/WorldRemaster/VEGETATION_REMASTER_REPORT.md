# Mesh Vegetation Remaster Report

> **Хронологическая пометка.** Это исторический отчёт об отключённом 49-cell
> mesh-painting инструменте июля 2026 года. Present-tense формулировки ниже не
> описывают активную карту. Активная v12 использует 88 компактных packed-cell
> сцен, 5,157,636 записей Forest grass и 67,979 ближних деревьев; полная
> генерация `20260902-001303` и свежая валидация `20260902-004112` прошли
> 88/88 и совпали по 268 детерминированным артефактам. Текущая фиксация находится
> в `MAP_VEGETATION_PRESENTATION_REVISION.md`; старые доказательства сохранены
> без переписывания.

## Результат

Реализована отдельная система рисования растительности по mesh-карте для
Unity 6 HDRP. Unity Terrain не используется. Существующая геометрия мира,
дороги, мосты, здания и streaming-сцены не изменялись.

Система включает:

- явные компоненты `VegetationSurface`, `VegetationBlocker` и
  `VegetationExclusionVolume`;
- 49 `VegetationCellAsset` — по одному на каждую ячейку действующего
  `ProductionWorldStreamingManifest`;
- world-space RGBA-маски 512×512 без зависимости от UV исходных мешей;
- каналы R/G/B/A: короткая, луговая, высокая и декоративная растительность;
- Scene View `EditorTool` с Paint, Erase, Smooth и Noise;
- обработку кистью всех пересечённых ячеек и единый Undo/Redo на штрих;
- перестройку только затронутых тайлов 32×32 м после MouseUp;
- детерминированную глобальную jittered-grid генерацию со stable hash;
- проверку точной поверхности, blocker-геометрии, воды, уклона, высоты и
  exclusion volumes;
- компактные 24-байтовые instance records без GameObject на куст травы;
- GPU-рендер через `Graphics.RenderMeshIndirect`;
- отдельные tight bounds на каждый тайл и профиль;
- near/middle/far LOD с dither-переходами;
- HDRP alpha clip, depth, shadow caster, wind, motion vectors, instance color
  variation и wind phase;
- режимы отладки масок, blockers, dirty tiles, candidates, rejection reasons,
  количества instances и LOD/culling bounds.

## Данные

Сборщик создал:

- 49 streaming-cell assets;
- 49 PNG density masks;
- 49 компактных Undo state assets;
- 4 vegetation profiles;
- общий catalog, runtime prefab, материал и три LOD-меша.

Пустой полный набор занимает около 0,93 МиБ. Старые нативные texture assets
размером около 103 МиБ удалены миграцией без потери содержимого масок.

## Проверки

- Unity batch asset build:
  `MESH_VEGETATION_ASSET_BUILD_OK cells=49 profiles=4 mask=512 tile=32m`;
- фокусные EditMode-тесты: 6/6 passed;
- конфигурация catalog: 49 уникальных ячеек и четыре уникальных канала;
- размер `VegetationInstanceRecord`: 24 bytes;
- компиляция C# и нового HDRP shader прошла в Unity 6000.3.11f1.

## Совместимость

Система добавлена как независимый слой `MSC.World.Vegetation`. Она не заменяет
существующий Phase 1 forest baseline и не меняет streaming API. Старую
растительность следует выключать только после ручной проверки нарисованного
покрытия и производительности в Bootstrap.

## Оставшаяся ручная авторизация карты

По политике сохранности мира сборщик не назначал компоненты существующим
мешам автоматически. Автор должен явно отметить ground meshes как surfaces,
а дороги, здания, воду, фундаменты, bridge decks и pillars — как blockers,
после чего добавить сгенерированный runtime prefab в контролируемый world
presentation/override слой и нарисовать маски.

Полная последовательность приведена в
`Docs/WorldRemaster/MESH_VEGETATION_PAINTING_SYSTEM.md`.

## Решение по фазам от 2026-07-26

Деревья условно приняты как временный Phase 1 baseline. Их окончательная
плотность, видовой состав, цвет, LOD, коллизии и ручная чистка относятся к
Phase 2.

Система mesh vegetation painting сохраняется как отключённый project-owned
инструмент. Полное покрытие карты травой, художественная калибровка профилей и
замена существующей растительности выполняются только после одобрения Phase 1
gate. Runtime prefab не должен подключаться к Bootstrap до этого решения.

Подробный порядок будущей работы:
`Docs/Phase2/GRASS_AUTHORING_PLAN_RU.md`.
