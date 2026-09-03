# Phase 2 backlog — Production Remaster and Polish

Phase 2 начинается только после user-approved Phase 1 gate. Пункты ниже не
могут вытеснять required parity rows.

## World и art

- cell-by-cell production terrain/roads/ditches/shoreline replacement;
- новые production buildings, props, vegetation, LODs и collision proxies;
- условно принятый Phase 1 forest baseline: финальный видовой состав, плотность,
  единый цвет, LOD/culling, корректные стволовые коллизии и ручная очистка
  дорог/зданий;
- полное mesh-based травяное покрытие через `MSC.World.Vegetation`: разметка
  surfaces/blockers, world-space RGBA masks, художественные профили, покраска
  всех streaming-ячеек и performance acceptance по
  `Docs/Phase2/GRASS_AUTHORING_PLAN_RU.md`;
- устранение sprite forests, terrain voids и under-map hacks без расширения
  gameplay footprint;
- новые HDRP PBR materials/textures, decals, dirt, rust, wetness и puddles;
- production Satsuma и полный production vehicle roster;
- newly authored production character meshes, UVs, HDRP materials/textures,
  rigs, animation and viewmodel arms; Phase 1 donor-derived geometry,
  materials/textures and clips are all replaced through their independent
  production replacement keys;
- финальная signage/labels/typography с чистым provenance.

## Presentation и audio

- полностью reauthored soundscape и финальный Wwise mix;
- production UI font/logo/icon art без изменения accepted 08A layout;
- расширенная indoor/outdoor acoustics и zonal polish;
- cinematic/presentation polish, не влияющий на gameplay authority.

## Systems, только после parity

- conditionally accepted NPC/story-traffic remaster pass: refine Jani, Petteri,
  Pena/FITTAN and bus route lines, recovery, physical handling and drift; finish
  vehicle/Wwise audio, animation and presentation polish; fix regressions found
  during full remaster playthroughs while preserving the accepted Phase 1
  stable IDs, save DTOs, route evidence and project-owned runtime authority;
- расширенные seasons и weather variety;
- dynamic bodywork/paint/rust/deformation сверх donor behavior;
- улучшенный NPC memory/AI сверх locked donor version;
- кандидат улучшения Теймо после падения с велосипеда: восстановление состояния
  через `Fallen -> Remount` при исправном/доступном велосипеде либо
  `Fallen -> Walk` с продолжением расписания пешком вместо постоянной спящей
  куклы; решение не активируется до user-approved Phase 2 gate;
- новые районы, jobs, NPC, story и activities;
- новые accessibility/remake-only helpers по отдельному approval;
- multiplayer/mod SDK/console/VR только по новой product decision.

## Запрещённые shortcuts

AI-upscale donor textures, автоматическое subdivision donor meshes и простое
переименование `TemporaryDirectImport` не считаются production replacement.
