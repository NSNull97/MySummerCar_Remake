/plan

# MILESTONE 05C1 — ОГРАНИЧЕННЫЙ БАЗОВЫЙ СЛОЙ НЕПРЕРЫВНОЙ ЗЕМЛИ И КОЛЛИЗИИ У ТЕЙМО

Полностью прочитай `AGENTS.md` до начала работы.

Также прочитай `Prompts/CURRENT_STATE.md` и `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

Также полностью прочитай:

- `Prompts/05C_FULL_MAP_GEOMETRY_EVALUATION.md`;
- `Docs/Milestones/MILESTONE_05C_REPORT.md`;
- `Docs/WorldTransfer/M05C_GEOMETRY_EVALUATION_COVERAGE.md`;
- `Docs/WorldTransfer/M05C_INSPECTION_CHECKLIST.md`;
- `Docs/WorldRemaster/TERRAIN_REMASTER_REPORT.md`;
- `Docs/WorldRemaster/COLLISION_REMASTER_REPORT.md`;
- `Docs/WorldRemaster/STREAMING_INTEGRATION_REPORT.md`;
- текущие `git status` и `git diff`.

## Цель

Создать один ограниченный технический пилот непрерывной земли и статической
коллизии для подтверждённой пустоты donor-карты за визуальной границей леса
возле магазина Теймо.

Пилот закрывает только регион `M05C1-VOID-TEIMO-001`, классифицированный как
`IntentionalDonorVoid`, и только его части в:

- `cell_-4_0`;
- `cell_-3_0`.

Это safety/topology baseline. Он предотвращает отсутствие основания за
старой визуальной границей, но не открывает новую поддерживаемую gameplay-зону
и не является ремастером рельефа всей карты.

## Зафиксированная граница

Включить:

- project-owned реестр региона и двух cell-owned частей;
- явно записанные station-профили границ и высот;
- project-authored low-detail mesh земли;
- один статический `MeshCollider` на каждую часть;
- существующий project-owned terrain material;
- project-owned stable ID и authoring fingerprint каждой части;
- отдельную generated-сцену для каждой из двух ячеек;
- детерминированную сборку, строгую валидацию и reference captures;
- ручной checklist для Scene View и коллизии.

Исключить:

- заполнение других дыр или всей внешней области карты;
- изменение frozen 04A1/05C reference data и reference meshes;
- удаление или замену donor tree-card/boundary geometry;
- дороги, здания, интерьер магазина, воду, shoreline и инфраструктуру;
- vegetation remaster, LOD/HLOD и финальные материалы;
- Player, Interaction, NPC, traffic, quests, economy и survival;
- vehicle assembly или vehicle simulation;
- weather, lighting, audio, UI и save;
- 05A zone batch, 05B validation и Milestone 06 внутри этого этапа.

## Классификация пустот

Использовать явные значения:

- `IntentionalDonorVoid` — пустое основание внутри ограниченного пилота,
  скрытое donor boundary geometry; только этот класс разрешено заполнять;
- `WaterOrShoreline` — вода, дно или берег; не заполнять этим baseline;
- `ExternalWorldBoundary` — внешняя бесконечная область; вне scope;
- `UnknownGap` — неоднозначный участок; остановиться и записать blocker.

Нельзя автоматически превращать неизвестную дыру в `IntentionalDonorVoid`.

## Контракт данных

Источник авторинга:

`Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv`

Для каждой из двух частей обязаны быть записаны:

- `RegionId` и уникальный `PieceId`;
- project-owned `StableEntityId`;
- `Classification=IntentionalDonorVoid`;
- `ImplementationStatus=ApprovedBoundedPilot`;
- точный `CellId`;
- упорядоченный station-профиль;
- максимальный продольный шаг;
- surface offset;
- water-mask status;
- stable IDs геометрии, подтверждающей границу;
- project-owned mesh и scene destinations;
- SHA-256 authoring fingerprint;
- примечание об ограничениях.

Генератор не должен принимать строки с неверным fingerprint, повторяющимся
stable ID, нечисловыми координатами, неупорядоченными станциями, нулевой
шириной или путями вне разрешённых project-owned каталогов.

## Визуальная и пространственная верность

Продолжение террейна должно устранять техническую пустоту, но сохранять
оригинальные дороги, силуэты, линии обзора и игровую границу района Теймо.
Нельзя превращать пустоту в новую авторскую локацию. Для проверки использовать
реальные скриншоты/геометрию оригинала, а не концепты ремейка.

## Геометрический контракт

- Y-up; `1 Unity unit = 1 m`.
- Полный bounded AABB региона: `x=[-1742, -1447.7]`, `z=[100, 225]`.
- Межъячеечная граница: `x=-1536`.
- Каждая часть принадлежит ровно одной 512-метровой ячейке.
- Соседние части используют совпадающую геометрию на seam.
- Не создавать один mesh, пересекающий обе ячейки.
- Все vertices, normals, tangents, UV и bounds должны быть конечными.
- Root каждой generated-сцены стоит в origin своей ячейки; mesh хранит
  cell-local координаты.
- Разрешена только документированная интерполяция между station-профилями.
- Геометрия должна оставаться low-detail baseline, а не имитировать final art.

## Коллизия и runtime boundary

- На каждой части нужен статический non-convex `MeshCollider` без `Rigidbody`.
- Collider использует тот же mesh, что и визуальная поверхность.
- `WorldVoidFillMarker` обязан сообщать правильные region/piece/cell IDs,
  classification и fingerprint.
- `SafetyTopologyBaseline=true` и `OpensGameplayArea=false`.
- Никакой runtime-зависимости от `LegacyImport/ReferenceOnly`, donor meshes,
  donor textures, external staging или Editor assemblies.
- Generated void-fill scenes остаются исключены из обычных Build Settings до
  отдельного integration-решения в 05B.
- Не добавлять новые gameplay scripts и не менять существующую архитектуру
  Player/Interaction.

## Инструменты Editor

Предоставить команды:

- `Tools > MSC Remake > World Transfer > 05C1 > Rebuild Teimo Continuous Ground Baseline`;
- `Tools > MSC Remake > World Transfer > 05C1 > Validate Teimo Continuous Ground Baseline`;
- `Tools > MSC Remake > World Transfer > 05C1 > Open Teimo Reference + Fill Review`;
- `Tools > MSC Remake > World Transfer > 05C1 > Capture Teimo Void Evidence`;
- `Tools > MSC Remake > World Transfer > 05C1 > Capture Teimo Reference + Fill Review`.

Reference captures являются доказательством авторинга и не входят в runtime.

## Acceptance criteria

- В approved scope ровно один region ID и ровно две части.
- Ячейки представлены ровно один раз: `cell_-4_0` и `cell_-3_0`.
- Обе строки имеют класс `IntentionalDonorVoid`, уникальные stable IDs и
  совпадающие с содержимым SHA-256 fingerprints.
- Полный generated AABB остаётся внутри зафиксированной границы пилота.
- Каждая часть остаётся внутри своей ячейки, а seam на `x=-1536` не имеет
  геометрического разрыва или несовпадения высоты.
- Meshes содержат конечную геометрию, корректные triangles и ненулевые bounds.
- Обе generated-сцены содержат renderer, project-owned material, статический
  collider, marker и stable identity.
- Downward collision probes подтверждают покрытие обеих частей и seam.
- Суммарная deterministic geometry содержит `104` vertices и `100` triangles;
  seam содержит `26` совпадающих пар vertices с допуском `0.0001 m`.
- Все `100` triangle-centroid collision probes дают hits `100/100` с
  допуском точки `0.02 m`; максимальный уклон не превышает `15°`.
- Ни дорога, ни магазин, ни вода не перекрыты новой землёй.
- Production/baseline assets имеют ноль donor/reference-only dependencies.
- Обе generated fill-сцены не включены в обычный build до ручной приёмки и
  последующего 05B integration gate.
- Повторная сборка при неизменном CSV даёт те же authoring и geometry hashes.
- Компиляция, строгий validator и доступные focused tests проходят либо точные
  blockers записаны в отчёте.
- Ручное визуальное и collision approval не объявляется без проверки
  пользователя; до неё статус остаётся `manual gate pending`.

## Обязательные outputs

Создай или обнови только относящиеся к 05C1 implementation файлы и:

- `Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv`;
- `Docs/WorldRemaster/M05C1_CONTINUOUS_GROUND_COVERAGE.md`;
- `Docs/WorldRemaster/M05C1_INSPECTION_CHECKLIST.md`;
- `Docs/Milestones/MILESTONE_05C1_REPORT.md`.

Разрешены минимальные status/provenance updates в `Prompts/README.md`,
`Docs/ROADMAP.md`, завершённом 05C sign-off и ledgers. Не переписывай документы
других систем и не используй эти updates для расширения scope.

## Stop condition

Остановись после bounded Teimo pilot, доступных автоматических проверок,
отчёта и точных инструкций ручной проверки.

Не расширяй 05C1 на другие пустоты. Следующий и только следующий этап после
ручного принятия 05C1 — `Prompts/05B_WORLD_VALIDATION.md`.
