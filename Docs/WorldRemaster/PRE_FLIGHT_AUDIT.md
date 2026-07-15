# World Remaster 05A — Pre-flight Audit

Дата: 2026-07-14. Unity: `6000.3.11f1`; HDRP: `17.3.0`.

## Проверено

- Полностью прочитаны `AGENTS.md`, `Prompts/05A_WORLD_REMASTER.md`, отчёты 04A/04B/05 и обязательные world-transfer документы.
- Canonical 04A1 database сохранена без изменений: `13 509` entity records, `49` spatial cells плюс `global`, `512 m` cell size.
- Donor/reference слой остаётся под `Assets/Game/World/Content/WorldTransfer` и `LegacyImport/ReferenceOnly`; production расположен отдельно под `Assets/Game/World/Production`.
- Unity доступна по `C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe`.
- Blender не найден в `PATH`; сторонние DCC/packages не устанавливались.
- M4 player prefab и M05 assembly scene доступны и компилируются.

## Выявленная неоднозначность

Frozen 04A1 запись `fb0f962be1b325cc19296c66751818c0` помечает `CABIN/Shed/garage_shed_roof` как `PrimaryHomeGarage` в `cell_0_0`. Donor hierarchy отдельно содержит фактический дом/гараж игрока `YARD/Building/Garage` в `cell_0_-3`, примерно у `(153.495, 0.95, -1033.23)` в project-world coordinates.

05A не переписывает исторический provenance. Пилотом выбран `cell_0_-3`; неоднозначная 04A1 метка остаётся audit finding для будущей data migration, а не молча исправленной историей.

## Найденные prerequisites

- coordinate convention: Y-up, `1 Unity unit = 1 m`;
- стабильные reference IDs и cell assignment;
- M3 project-authored HDRP texture basis;
- M4 Player/Interaction и Crossdot;
- M05 vehicle assembly controller, 15 parts / 14 mounts;
- build/test entry points Unity batch mode.

## Missing/manual gates

- точный donor terrain heightfield и spline/road centerline для home yard;
- production DCC source meshes и unique final texture scans;
- full-house interior production geometry;
- GPU/FPS/VRAM standalone capture;
- manual Game/Scene View visual acceptance и capture matrix;
- Blender/Substance-equivalent authoring for final hero assets.

Donor installation не открывалась на запись и не использовалась как runtime dependency.
