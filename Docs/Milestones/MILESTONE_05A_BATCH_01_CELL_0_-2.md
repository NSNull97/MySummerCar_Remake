# Milestone 05A — Batch 01 `cell_0_-2 / HomeShorelinePier`

Status: **ProductionCandidate — automated validation and traversal/water/hedge manual gates passed; visual-reference/performance gates remain**

## Selection record

- Selected before production edits: `cell_0_-2`, bounded sub-zone `HomeShorelinePier`.
- Selection rule: next vertical-slice dependency. This is the direct spatial continuation of the reviewed home/garage pilot in `cell_0_-3`.
- Included: home pier, its authored collision references, pontoons, a bounded lake/bottom presentation for the cell, and the three yard hedge records that cross into this cell.
- Excluded: `MISC/MAITO`, garbage collector/backup objects, quests, swimming, water simulation, weather, and every other world cell.
- Milestone boundary: only the next 05A world-remaster batch; no 05B or 06 work.

## Frozen source revision

- Database: `Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json`
- Database version: `04A1.1`
- Database SHA-256: `706A4303A715539AC2D45A5AB0DFF487B685BFA3F336C57A10A477A45A454FA8`
- Donor scene provenance: `mysummercar_Data/level2`, SHA-256 `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31`
- Coordinate convention: `1 Unity unit = 1 metre`; 512 m streaming cells; stable IDs and converted world transforms are read from the frozen 04A1 tables.

## Pre-flight

- Licensed donor installation: available and treated as read-only.
- External donor staging and decompiled reference: available outside Git.
- Local path configuration: available and ignored by Git.
- Reference media root: available; no dedicated shoreline capture is present, so direct visual parity remains a manual gate.
- Reusable project assets: existing HDRP materials, neutral-lighting prefab, M4 player architecture, world-cell registry, stable-ID authoring and comparison-mode tooling.
- External DCC/package changes: not required for this bounded first-pass production candidate.

## Bound reference records

| Stable ID | Reference path | Production intent |
|---|---|---|
| `345dc7662dae9f1f01d77b15f74e5f8f` | `MAP/PierHome/pier` | newly authored pier deck and supports |
| `56a7aa7c66146248d6c820c31a6b99fd` | `MAP/PierHome/pier_pontons` | newly authored pontoons |
| `de5d5682cbef7d27a48a473d5e85877d` | `MAP/PierHome/coll` | explicit walkable collision coverage |
| `e0fa39e1ceeed93727dd86e749c6d115` | `MAP/PierHome/coll` | explicit walkable collision coverage |
| `b412961b75cb019e74a83b24faac32a4` | `MAP/LakeNice/Lake/Tile` | bounded project-authored water presentation |
| `f700b12cf5c75a3906dd079acea3f274` | `MAP/Bottom` | bounded lake-bottom/shore transition |
| `b8de7336e204fae3ba333227b3e94d19` | `YARD/Building/LOD/HedgeFence/hedge` | project-authored hedge with LOD/collision |
| `449b18de0c10f87887e3f3304a90366e` | `YARD/Building/LOD/HedgeFence/hedge` | project-authored hedge with LOD/collision |
| `847f56ce8c1be238f4bcae514bb55fdf` | `YARD/Building/LOD/HedgeFence/hedge` | project-authored hedge with LOD/collision |

## Acceptance criteria

- Exactly the nine listed stable IDs receive production bindings; the other six `cell_0_-2` records remain explicit backlog.
- Production content has no dependency on `LegacyImport/ReferenceOnly` or `Imported/DonorGenerated`.
- The cell scene rebuilds deterministically and is assigned to `cell_0_-2` from its frozen pier anchor.
- Pier anchor deviation is at most `0.05 m`; hedge anchor deviation is at most `0.05 m`.
- Pier has continuous player collision; water has no blocking collider; hedges have simplified collision and at least two LOD levels.
- Production materials are HDRP-compatible and use only project-authored material/texture inputs.
- Batch scene is enabled in Build Settings; comparison/debug scene remains excluded.
- Static validation, focused EditMode tests and focused PlayMode tests pass.
- Reference-only, production-only and overlay captures plus a capture manifest are generated. Direct donor visual review and real GPU/FPS profiling remain honestly reported manual gates.

## Реализация

- Добавлены project-authored prefabs:
  - `Assets/Game/World/Production/Prefabs/WR_HomePier.prefab`;
  - `Assets/Game/World/Production/Prefabs/WR_HomeShorelineWater.prefab`;
  - `Assets/Game/World/Production/Prefabs/WR_HedgeSegment.prefab`;
  - `Assets/Game/World/Production/Prefabs/WR_HomeShorelinePier.prefab`.
- Пирс состоит из 14 досок с явной walkable collision, опор, двух понтонов и переходной доски. Максимальный зазор между соседними plank-collider — `0,08 м`, допуск — `0,1 м`.
- Вода создана заново на HDRP/Lit-материале без blocking collider. После review capture южная граница воды была отодвинута от дворовых изгородей; overlap у границы `cell_0_-2` устранён.
- Shore approach и пешеходная дорожка соединяют принятый home-yard контекст с порогом пирса. При первой ручной проверке обнаружен `SEAM-001`: прежняя геометрия не доходила до края terrain `cell_0_-3` на `3,23 м` (approach) и `4,23 м` (footpath). Подход продлён до взаимного перекрытия, высота дорожки выровнена со склоном, а непрерывность закреплена статической и PlayMode-проверками. Это first-pass геометрия, а не заявка на финальный ландшафт.
- Три hedge instance стоят на точных converted anchors 04A1, используют вновь созданную геометрию, simplified `BoxCollider` и двухступенчатый `LODGroup`.
- Сгенерированы streaming-cell `Production_cell_0_-2.unity`, отдельная playtest-сцена и исключённая из билда comparison-сцена с тремя фиксированными камерами (`Pier`, `Hedge`, `Seam`).
- В playtest/comparison соседний `cell_0_-3` используется только как принятый контекст. Production-cell `cell_0_-2` остаётся самостоятельной и не получает cross-cell hierarchy dependency.
- Player/Interaction, M05 vehicle assembly и donor installation не изменялись.

## Registry, provenance и coverage

- Registry version: `05A.2`; reference records: `13 509`; production bindings: `33`.
- Batch 01: `9 / 15` records (`60,000%`).
- Осталось в `cell_0_-2`: 6 explicit backlog records — garbage collector/backup и `MISC/MAITO` gameplay objects.
- Global unassigned records: `13 476`; grouped art tasks: `261`.
- Статус ячейки: `ProductionCandidate`; `Approved`/`Verified` не выставлялись.
- Production dependencies на `LegacyImport/ReferenceOnly` и `Imported/DonorGenerated`: `0`.
- Классификация: `WorldLayoutReference` для frozen anchors и `ReauthoredGeometry`/`ReauthoredMaterial` для новых production assets.

## Captures и производительность

В ignored-каталоге `PerformanceCaptures/Milestone05A/Batch01_cell_0_-2/` сгенерированы:

- `Pier_ReferenceOnly.png`, `Pier_ProductionOnly.png`, `Pier_OverlayComparison.png`;
- `Hedge_ReferenceOnly.png`, `Hedge_ProductionOnly.png`, `Hedge_OverlayComparison.png`;
- `Seam_ReferenceOnly.png`, `Seam_ProductionOnly.png`, `Seam_OverlayComparison.png`;
- `capture_manifest.csv` с Unity `6000.3.11f1`, builder `05A.2`, камерами, FOV и разрешением `1920 × 1080`.

Статический вклад Batch 01: 38 renderers, 30 colliders, 3 LOD groups. Совокупный bounded 05A audit: 370 renderers, 164 colliders, 67 LOD groups и 159 548 instance-counted triangles. Реальный CPU/GPU frame time, draw calls, VRAM и 60 FPS в standalone build не измерялись; performance approval не заявлен.

## Проверки

- Unity batch build: PASS, `WORLD_REMASTER_05A_BUILD_OK version=05A.2 ... mapped=33`.
- Два последовательных rebuild: PASS; SHA-256 `Production_cell_0_-2.unity` оба раза `973757687E6A24EF098D32BECB5595B24A2E9A336B77180F821CA64BA7BFD89F`.
- `ProductionCellValidationTool.RunBatch`: PASS, 0 errors / 0 warnings; home-to-pier seam overlap — `3,75 м` при минимуме `1 м`.
- Focused EditMode `MSC.Tests.EditMode.WorldRemaster`: PASS `16/16`.
- Focused PlayMode `MSC.Tests.PlayMode.WorldRemaster`: PASS `8/8`; новый тест проверяет walkable collision с шагом `1 м` на всём 76-метровом маршруте от home terrain до порога пирса.
- Full PlayMode: PASS `25/25`.
- Full EditMode baseline: `117/120`; после seam-fix повторно выполнен scoped EditMode `16/16`. Остались три известные baseline-проблемы вне scope:
  - два M3 lighting failure, связанные с пользовательским изменением `M3_NeutralVolume.asset`;
  - известный drift 04A1 donor-hash dry-run.

## Ручная проверка

Результаты пользователя от 2026-07-15:

- PASS — вода не создаёт невидимую преграду; в неё можно упасть;
- PASS — три изгороди не висят и не пересекаются с соседней геометрией;
- FAIL → FIXED → PASS — обнаруженный разрыв между домашней зоной и подходом к пирсу (`SEAM-001`) исправлен; пользователь повторно прошёл стык и подтвердил результат 2026-07-15.

1. Открыть `Assets/Game/World/Production/Scenes/WorldRemasterHomeShorelinePlaytest.unity`.
2. Повторный проход через исправленный стык от края домашнего terrain на дорожку и обратно выполнен: щель, резкая ступенька и падение не воспроизвелись.
3. Проверка non-blocking воды выполнена; повторять её для принятия `SEAM-001` не требуется.
4. Проверка положения изгородей выполнена; pop/LOD остаётся отдельным визуальным наблюдением при необходимости.
5. Открыть `Assets/Game/World/Debug/Comparison/WR_HomeShorelineComparison.unity`, переключить `ReferenceOnly`, `ProductionOnly`, `OverlayComparison` и сравнить proxy anchors.
6. Для visual approval нужен отдельный shoreline reference capture оригинальной игры; текущие proxy captures подтверждают структуру, но не художественную идентичность.
7. Для performance approval нужен Windows x64 Development Build и фактический capture 1920 × 1080 на целевом ПК.

## Ограничения и риски

- Геометрия, материалы, shoreline и vegetation — first production candidate, не final art.
- Donor bounds для девяти записей вырождены до pivot points, поэтому размерная parity основана на anchors и ручном visual review.
- Вода не реализует волны, плавание, underwater, foam, weather/wetness response или cross-cell simulation.
- Lighting остаётся существующим neutral baseline; текущий эксперимент пользователя в `M3_NeutralVolume.asset` сохранён без изменений этой партии.
- Milk/garbage gameplay, quests и другие зоны не затронуты.

## Следующая рекомендация

После ручного принятия этой production candidate выполнить ровно один следующий bounded `05A Batch 02` для следующей зависимой world-zone; не переходить к 05B/06 в рамках текущего отчёта.
