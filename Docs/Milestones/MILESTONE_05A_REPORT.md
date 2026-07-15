# Milestone 05A Report — bounded World Remaster pilot

Дата: 2026-07-14. Режим: `ProductionReplacementWithReferenceParity`. Builder: `05A.1`. Unity: `6000.3.11f1`, HDRP `17.3.0`.

## 1. Executive summary

Первый ограниченный запуск 05A завершил production-replacement pipeline и одну репрезентативную пилотную зону `cell_0_-3` (дом, гараж и двор). Созданы раздельные reference/production/comparison слои, полный registry для 13 509 reference-записей, статус всех зон, структурированный ручной арт-бэклог, dashboard, детерминированный production-cell, сравнение и целевые тесты.

Вся карта **не** объявляется отремастеренной. Прямо связаны 24/13 509 записей (0.178%); в пилотной исходной ячейке — 24/671 (3.577%). Четыре записи имеют статус `ProductionCandidate`, двадцать — `FirstPass`, 13 485 — `Unassigned`; `Approved` и `Verified` отсутствуют до ручной приёмки.

## 2. Prerequisites и reference status

Найдены: Unity/HDRP foundation, frozen world database `04A1.1`, уникальные project-owned stable IDs, 512-метровое разбиение, M4 Player/Interaction, M05 Vehicle Assembly и batch-mode Unity. В базе 13 509 geometry records и 51 обнаруженная группа зон/служебных областей.

Не хватает production DCC-источников, точного terrain heightfield/road spline для домашнего двора, финальных UV/текстур, полного интерьера, GPU/VRAM standalone capture и ручной Unity-приёмки. Blender отсутствует в `PATH`; новые инструменты и пакеты не устанавливались.

Frozen 04A1 помечает `CABIN/Shed` в `cell_0_0` как `PrimaryHomeGarage`, тогда как фактический home/garage обнаружен в `YARD/Building/Garage`, `cell_0_-3`. История 04A1 не переписана: неоднозначность записана в pre-flight audit.

## 3. Production architecture

- `MSC.World.Remaster.Runtime`: registry DTO/ScriptableObject, статусы, сравнение режимов, pilot marker и moving-architecture interaction capability.
- `MSC.World.Remaster.Editor`: registry/backlog generator, deterministic mesh/prefab/cell builder, dashboard, comparison commands, visual capture и validator.
- Layer A: removable reference metadata/proxies.
- Layer B: новые project-authored production assets.
- Layer C: отдельная comparison scene с `ReferenceOnly`, `ProductionOnly`, `OverlayComparison`.

Production prefabs не зависят от `LegacyImport/ReferenceOnly`, `Imported/DonorGenerated`, donor meshes/textures, donor executable или старых Unity assemblies. M4 Player/Interaction и M05 Assembly сохранены как отдельные потребители; object-name dispatch не добавлен.

## 4. Registry, zone status и backlog

Registry asset: `Assets/Game/World/Authoring/ReplacementProfiles/WR_WorldProductionAssetRegistry.asset`.

- записей: 13 509;
- direct bindings: 24;
- `ProductionCandidate`: 4;
- `FirstPass`: 20;
- `Unassigned`: 13 485;
- manual-art tasks: 263;
- zone rows: 51;
- unassigned rows без backlog dependency: 0.

Машиночитаемые источники правды: `WORLD_REPLACEMENT_LEDGER.csv`, `WORLD_ART_BACKLOG.csv`, `ZONE_REMASTER_STATUS.csv`. Dashboard доступен через `Tools > MSC Remake > World Remaster > Open World Remaster Dashboard`.

## 5. Pilot zone

Выбран `cell_0_-3`, anchor `(153.495, 0.95, -1033.23)`, Y rotation `180°`. Реализованы:

- 160 × 130 m first-pass terrain, home pad и дренажная канава;
- 160 m gravel-road context и 30 m driveway;
- garage/house exteriors, крыши, окна и representative interior slice;
- 2 гаражные створки, side door, house door и 2 gate leaves;
- workbench, shelves, crate, drum, mailbox, firewood, poles/wires и yard props;
- 64 детерминированных spruce instances с двумя LOD stages;
- 134 colliders, 332 renderers, 64 LOD groups;
- M4 player playtest и M05 assembly scene integration.

Пилот является production-candidate **для pipeline и композиции зоны**, но не завершённым raw-object replacement и не final art.

## 6. Status по системам

### Terrain и roads

Пилотные terrain/road/driveway/ditch meshes и HDRP materials создаются детерминированно. Garage anchor и cell ownership сохранены. Полный terrain height parity и road centerline parity не доказаны; direct coverage `Terrain 0/1`, `Road 0/29`, `Water 0/12`. Cross-cell seams, shoulders, junctions, splines, puddles и production layers отложены.

### Building exterior и interior

Новые garage/house shells независимы от donor payload. Direct coverage: exterior 15/1 985, interior 3/2 019, roof 2/160, door 1/193, window 1/120. Garage clearance fixture: opening 3.12 × 2.22 m против representative vehicle 2.20 × 1.75 m. Полные измерения комнат, authored topology/UV/bakes/trims и light-leak review остаются ручными задачами.

### Moving architecture

Шесть элементов остаются отдельными от static mesh, имеют hinge pivot, Y-axis range, collider и существующий `IContextInteractionTarget`. Автоматически проверены наличие и fit; ручные swing arc, obstruction, handle, save-state и sound-hook проверки не выполнены.

### Props, vegetation, water, infrastructure

Reusable pilot props и 64-tree placement подтверждают pipeline, но raw coverage `StaticProp 0/531`, vegetation categories `0/26`. Растительность — очевидный first-pass capsule proxy. Water ограничен ditch preview. Infrastructure coverage: `Wire 2/4`, `UtilityPole 0/14`; map-scale cable spline ещё не создан.

### Materials, textures и geometry

Создано 14 WR HDRP material assets на project-authored M3 procedural texture basis. Donor textures и AI-upscaled donor textures не используются. `MATERIAL_STANDARD.md`, `TEXTURE_STANDARD.md`, `GEOMETRY_STANDARD.md` фиксируют HDRP mask packing, SI scale, pivot, collision и LOD правила. Unique production textures, UVs, bake maps, decals и hero geometry остаются backlog.

### Collision и LOD/HLOD

В пилоте 134 newly authored colliders и 64 двухступенчатых tree LOD groups. Dynamic non-convex donor collision не используется. Building/prop LOD chains, billboards, HLOD/cell proxies и driving-speed pop review не реализованы и не заявлены как готовые.

### Streaming

`ProductionWorldCellBuilder` создаёт `Production_cell_0_-3.unity` и обновляет существующий cell in-place, сохраняя scene stable IDs/file IDs. Два последовательных rebuild дали одинаковый SHA-256:

`C0FC69B0C21FE86C1C8435BAE7D47FB3A1A964327DCEFE28DA9AFB773CAFDD52`.

Только одна production cell создана. Cross-cell roads/water/wires, large-object ownership, unload timing и HLOD останутся следующим zone batch.

### Landmark, player и vehicle fit

Home/garage anchor и zone assignment связаны с reviewed fixtures. Metadata overlay работает, но sparse reference proxies не дают visual donor-mesh fidelity. Автоматические clearance и collider fixtures проходят. Реальный player walkthrough, door/stair navigation, gate opening и быстрый vehicle approach в Unity ещё не приняты вручную.

### Lighting и weather compatibility

HDRP materials и neutral lighting подключены; runtime weather не добавлялся. 1920 × 1080 comparison captures просмотрены: режимы различимы, но production кадр слишком тёмный для art approval. Существующая рабочая правка `M3_NeutralVolume.asset` использует sky type `1`, тогда как frozen M3 test ожидает Physical Sky type `4`; 05A её не перезаписывал.

## 7. Performance

Статический Editor audit пилотного prefab:

| Metric | Value |
|---|---:|
| Renderers | 332 |
| Colliders | 134 |
| LOD groups | 64 |
| Instance-counted triangles | 158 548 |
| Unique mesh estimate | 0.18 MiB |

CPU/GPU frame time, draw calls, VRAM, peak memory, load/unload timing и standalone 1920 × 1080 FPS **не измерены**. Цель 60 FPS не объявляется достигнутой.

## 8. Coverage по категориям

| Category | Bound / total | Coverage |
|---|---:|---:|
| BuildingExterior | 15 / 1 985 | 0.756% |
| BuildingInterior | 3 / 2 019 | 0.149% |
| Roof | 2 / 160 | 1.250% |
| Door | 1 / 193 | 0.518% |
| Window | 1 / 120 | 0.833% |
| Wire | 2 / 4 | 50.000% |
| Bridge | 0 / 2 | 0% |
| ColliderOnly | 0 / 3 499 | 0% |
| Fence | 0 / 38 | 0% |
| Field | 0 / 3 | 0% |
| Floor | 0 / 41 | 0% |
| Gate | 0 / 1 | 0% |
| InteractivePropCandidate | 0 / 4 618 | 0% |
| Landmark | 0 / 12 | 0% |
| Road | 0 / 29 | 0% |
| RoadSign | 0 / 163 | 0% |
| Rock | 0 / 3 | 0% |
| SpawnMarker | 0 / 23 | 0% |
| StaticProp | 0 / 531 | 0% |
| Terrain | 0 / 1 | 0% |
| UtilityPole | 0 / 14 | 0% |
| VegetationBush | 0 / 7 | 0% |
| VegetationGrass | 0 / 3 | 0% |
| VegetationTree | 0 / 16 | 0% |
| Water | 0 / 12 | 0% |
| Yard | 0 / 12 | 0% |

Global: `24 / 13 509 = 0.17766%`.

## 9. Coverage по зонам

| Zone set | Bound / total | Coverage | Status |
|---|---:|---:|---|
| `cell_0_-3` | 24 / 671 | 3.577% | `ProductionCandidate`, manual review pending |
| Остальные 50 zone rows | 0 / 12 838 | 0% | `Unassigned`/explicit backlog |

Точные counts и status каждой из 51 зон находятся в `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`; агрегирование не скрывает ни одной ненулевой зоны — единственная такая зона `cell_0_-3`.

## 10. Validation и tests

- 05A builder: PASS (`mapped=24`).
- 05A validator: PASS, 0 errors, 0 warnings.
- World Remaster EditMode: **13/13 PASS**.
- World Remaster PlayMode: **5/5 PASS**.
- M4 Player/Interaction validator: PASS.
- M05 Vehicle Assembly validator: PASS.
- Full PlayMode: **22/22 PASS**.
- Full EditMode: **114/117 PASS**, 3 failures.
- CSV parse/completeness and `git diff --check`: PASS.
- Production donor-dependency scan: PASS, 0 violations.

Full EditMode failures:

1. Два M3 lighting tests фиксируют уже существующую правку neutral Volume (sky type 1 вместо ожидаемого 4); она не перезаписана в 05A.
2. Один 04A1 dry-run фиксирует известный hash drift `sharedassets3.assets/.resource` после переустановки donor. Frozen provenance не переписан.

Новых compiler errors нет. Полные результаты: `TestResults/Milestone05A_*` (ignored).

## 11. Visual validation

Локально созданы и просмотрены:

- `PerformanceCaptures/Milestone05A/ReferenceOnly.png`;
- `PerformanceCaptures/Milestone05A/ProductionOnly.png`;
- `PerformanceCaptures/Milestone05A/OverlayComparison.png`;
- `PerformanceCaptures/Milestone05A/capture_manifest.csv`.

Переключение режимов подтверждено. Reference view — только sparse metadata proxies. Тёмная экспозиция, capsule vegetation и отсутствие полного lighting/weather matrix не позволяют дать art approval. Ручной Game View walkthrough не выполнялся.

## 12. Files created and modified

Основные новые пути:

- `Assets/Game/World/Production/Runtime/`;
- `Assets/Game/World/Production/Materials/`, `Meshes/`, `Prefabs/`, `Scenes/`;
- `Assets/Game/World/Authoring/ReplacementProfiles/`;
- `Assets/Game/World/Generated/ProductionCells/`;
- `Assets/Game/World/Debug/Comparison/`;
- `Assets/Game/World/Editor/`;
- `Assets/Game/Tests/EditMode/WorldRemaster/`;
- `Assets/Game/Tests/PlayMode/WorldRemaster/`;
- `Docs/WorldRemaster/` и этот отчёт.

Обновлены assembly references тестов, `VehicleAssemblyPrototype.unity`, `EditorBuildSettings.asset`, architecture/roadmap/testing/performance и porting audit/matrix/system map/ledger. Существующие dirty изменения предыдущих milestones сохранены и не сброшены.

Generated local content: `PerformanceCaptures/Milestone05A/`, `Logs/Milestone05A_*`, `TestResults/Milestone05A_*`; они игнорируются Git. Production cell является project-owned reproducible content и намеренно находится в `Assets`.

## 13. Blockers, risks и manual art

Точные blockers:

- ручная player/vehicle traversal и door/gate swing приёмка;
- слишком тёмный neutral capture и незавершённая lighting matrix;
- sparse metadata reference layer вместо donor visual mesh comparison;
- 13 485 записей без direct production replacement;
- отсутствие DCC-final geometry/UV/bakes/textures;
- непроверенные terrain/road/water spatial fixtures;
- отсутствие standalone GPU/memory/streaming profile;
- frozen 04A1 hashes отличаются от переустановленного donor install.

Риски: распространение неверного garage/landmark label, масштабирование first-pass profiles до ручной приёмки, чрезмерная scene-object растительность, cross-cell seams, collider blockers и преждевременное использование pilot materials как final.

Manual Unity actions:

1. Открыть `WorldRemasterPilotPlaytest.unity`, пройти гараж/дом, открыть все шесть hinges и проверить лестницы/невидимые blockers.
2. Открыть `VehicleAssemblyPrototype.unity`, проверить driveway/garage clearance и полный install/fastener flow.
3. Проверить dashboard и comparison modes, затем снять clear/evening/overcast/rain/dusk/interior/headlight matrix.
4. После visual acceptance выполнить Development Player performance capture.

Manual Blender/Substance-equivalent actions: authored garage/house/interior/road/vegetation meshes, UVs, normal/AO/curvature/mask bakes, unique materials/decals, production LODs и collision proxies. Все группы отражены в 263 backlog tasks.

## Продолжение 05A — Batch 01 `cell_0_-2 / HomeShorelinePier` (2026-07-15)

Bounded baseline расширен одной зависимой подзоной без перехода к 05B/06. Builder/registry обновлены до `05A.2`; добавлены project-authored пирс, shoreline/water и три LOD-изгороди, отдельные production-cell, playtest и comparison scenes.

- coverage партии: `9 / 15` (`60,000%`), шесть unrelated gameplay records остаются backlog;
- общий registry: `33 / 13 509` direct bindings, `13 476` unassigned records, `261` art tasks;
- `cell_0_-2`: `ProductionCandidate`, automated validator PASS; ручные traversal/water/hedge gates прошли, visual-reference и performance review остаются pending;
- deterministic cell SHA-256 после двух rebuild: `973757687E6A24EF098D32BECB5595B24A2E9A336B77180F821CA64BA7BFD89F`;
- `SEAM-001` после ручного finding исправлен: footpath перекрывает край принятого terrain на `3,75 м`, непрерывность закреплена статическим validator и PlayMode raycast-проверкой;
- focused EditMode `16/16`, focused PlayMode `8/8`, full PlayMode `25/25`;
- full EditMode `117/120`: только три ранее известные baseline failure (два M3 lighting и 04A1 donor-hash drift), новых regression нет;
- девять comparison captures 1920 × 1080 (включая отдельный `Seam` view) и manifest созданы локально под `PerformanceCaptures/Milestone05A/Batch01_cell_0_-2/` и не входят в Git.

Полный scope, stable IDs, acceptance criteria, команды, результаты и ручные gates находятся в `Docs/Milestones/MILESTONE_05A_BATCH_01_CELL_0_-2.md`. Исходный `cell_0_-3` baseline не переоценён и не объявлен final art.

## 14. Recommended next milestone

Ровно один следующий milestone: продолжение 05A для следующей зоны после ручной приёмки пилота.

Файл: `Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md`.

Точный следующий Codex prompt:

> Прочитай AGENTS.md и полностью прочитай Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md. Продолжи World Remaster только для следующей приоритетной зоны, используя утверждённый 05A registry/pipeline. Не переходи к vehicle simulation, 06 или другим milestones. Обнови registry, zone status, backlog и отчёты, затем остановись.
