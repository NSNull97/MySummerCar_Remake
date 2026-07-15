# Milestone 05 — Vehicle Assembly Report

Дата: 2026-07-14
Статус: **завершён в границах `Prompts/05_VEHICLE_ASSEMBLY.md`**
Dataset: `04B.4`
Unity: `6000.3.11f1`

## Итог

Создан самостоятельный data-driven prototype сборки автомобиля. Он поддерживает pickup, deterministic mount preview, handoff/install, дискретную затяжку и ослабление, dependency/removal blockers, снятие, безопасные Rigidbody-переходы и versioned save DTO round trip.

Архитектура Player/Interaction M4 сохранена. Milestone 05A, 05B, 06 и подробная vehicle simulation не выполнялись.

## Что было проверено до реализации

- `AGENTS.md` и полный milestone prompt;
- `CURRENT_STATE_AFTER_04.md` и отчёты M00–M04B;
- фактические M4 capabilities, carry controller, candidate query, player interaction controller и stable identity;
- Save boundary и assembly definitions в архитектурных документах;
- dataset/fixture `representative-part-mount-pivot-v1`;
- world coordinate scale и garage clearance status;
- donor audit, system map, porting matrix и ledger;
- существующий project-authored `ReauthoredRearBrakeDrum.prefab`.

Assembly gate был открыт: обе P0 assembly requirements имели `Covered`, fixture — `Ready`.

## Реализованный runtime

Новая сборка `MSC.Vehicle.Assembly` содержит:

- definitions: `PartDefinition`, `MountPointDefinition`, `FastenerDefinition`, `ToolDefinition`;
- rules/value data: `PartCategory`, `PartCompatibilityRule`, `MountConstraint`, `MountPose`, `FastenerSize`, `ToolCompatibilityRule`;
- state: `PartInstance`, `PartRuntimeState`, `MountPointRuntime`, `FastenerInstance`, `FastenerState`;
- graph/operations: `AssemblyDependency`, `AssemblyGraph`, `AssemblyOperation`, `AssemblyOperationResult`, `AssemblyFailureReason`;
- orchestration: `VehicleAssemblyController`, `VehicleAssemblyQuery`, `VehicleAssemblyValidator`;
- M4 adapters: `AssemblyMountHandoffTarget`, `AssemblyFastenerInteractionTarget`, `AssemblyInstalledPartInteractionTarget`;
- preview/physics: `AssemblyMountPreviewPresenter`, `AssemblyMountObstruction`;
- persistence: `PartSaveDto`, `MountSaveDto`, `FastenerSaveDto`, `VehicleAssemblySaveData` schema 1.

Runtime assembly зависит только от `MSC.Core.Runtime` и `MSC.Interaction.Runtime`. Editor API и donor I/O отсутствуют.

## Representative vehicle

Builder создаёт 15 project-authored prototype parts и 14 mount points:

1. chassis root;
2. rear-left trailing arm;
3. rear-left brake drum;
4. rear-left wheel;
5. battery;
6. driver seat;
7. hood;
8. left door;
9. radiator;
10. engine block;
11. cylinder head;
12. starter;
13. alternator;
14. exhaust manifold;
15. intake manifold.

Все non-root parts имеют clean visual prefab, mount, fastener, tool rule, stable scene identity и explicit capabilities. Prototype scene:

`Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`.

Она использует существующий M4 player prefab и M3 neutral lighting. Dependency audit не нашёл ссылок на `LegacyImport/ReferenceOnly` или `Imported/DonorGenerated`.

## Rear-drum clean-room contract

| Элемент | Источник | M05 состояние |
|---|---|---|
| One BoltPM | dataset `04B.4` | один owned fastener |
| Tool | runtime video/static mapping | wrench `14` |
| Progression | donor serialized/runtime evidence | integer `0..8` |
| Remove gate | runtime repetitions | только stage `0` |
| Wheel blocker | static gate + user attestation | installed `vehicle.wheel_rl` блокирует снятие |
| Candidate marker | static evidence | `0.01 m` сохранён как provenance field |
| Position tolerance | project-authored | `0.42 m` |
| Angular tolerance | project-authored | `35°` |

Torque, физические обороты, stripping и continuous thread simulation не заявляются. Donor FSM/code не переносились.

## Editor tooling

Меню `Tools > MSC Remake > Vehicle Assembly` предоставляет:

- `Build Representative Test Vehicle`;
- `Validate Definitions and Prototype`;
- `Dependency Graph`;
- `Export Validation Report`;
- `Run Performance Audit`.

Mount authoring рисует position, forward и obstruction gizmos. Validator проверяет definitions, IDs, prefabs, graph cycles, ownership, tools, M4 wiring, build order, rear-drum fixture и donor dependency leaks.

## Сохранение

Schema v1 хранит stable part IDs, definition IDs, lifecycle, mount occupancy и fastener inserted/seated/stage state. Restore выполняет полный preflight и отклоняет unknown/duplicate/incompatible records, включая несовпадение occupancy и набора крепежей, до мутации graph.

Disk storage, slots, autosave, future migrations и original-save import не добавлялись.

## Производительность

Editor batch audit для 10 000 последовательных rear-drum mount queries:

- total: `262.093 ms`;
- mean: около `26.209 µs/query`;
- managed allocations: `0 bytes`;
- graph mutations: `0`;
- scene objects: `115`;
- renderers/colliders/rigidbodies: `59 / 45 / 15`.

Это Editor measurement, не GPU/player-build performance capture.

## Автоматические проверки

Финальные результаты:

- M05 builder: **PASS**, `15` parts / `14` mounts;
- M05 static validator: **PASS**;
- M05 focused EditMode: **22/22 passed**;
- M05 focused PlayMode: **8/8 passed**;
- full PlayMode regression: **17/17 passed**;
- full EditMode regression: **102/103 passed**;
- Foundation validator: **PASS**;
- M3 garage validator: **PASS**;
- M4 Player/Interaction validator: **PASS**;
- donor pipeline validator: **PASS**, 0 warnings;
- ReferenceCapture validator: **PASS**, 51 records, missing P0/P1 `20/6`, два известных non-fatal `UNUSED_DEPENDENCY` warnings;
- `git diff --check`: **PASS**, кроме информационного CRLF/LF warning для ledger.

Единственный full EditMode failure:

`MSC.Tests.EditMode.WorldTransfer.WorldTransferDataTests.Validation_DryRunMatchesDatabasePlan`.

Причина не относится к M05: текущая переустановленная donor game имеет другие hashes `sharedassets3.assets` и `sharedassets3.resource`, чем frozen 04A1 extraction provenance. Исторические 04A1 records намеренно не переписаны.

В ходе первого targeted прогона тестовый стенд дал `17/19` EditMode и `6/8` PlayMode. Исправлены только обнаруженные проблемы M05 fixture/integration: obstruction layer теста, проверка clean prefab path, сериализуемые MonoBehaviour/ScriptableObject file boundaries, Rigidbody warning и test carry positioning. Затем контракт крепежа усилен отдельными insert/remove, seated state, направлением поворота и preflight-проверкой occupancy; добавлены три EditMode теста. Финальные targeted suites полностью зелёные.

## Выполненные команды

Использовались Unity batch entry points:

- `MSC.Editor.VehicleAssembly.VehicleAssemblyPrototypeBuilder.RunBatch`;
- `MSC.Editor.VehicleAssembly.VehicleAssemblyPrototypeValidator.RunBatch`;
- `MSC.Editor.VehicleAssembly.VehicleAssemblyPerformanceAudit.RunBatch`;
- `MSC.Editor.VehicleAssembly.VehicleAssemblyPrototypeValidator.ExportValidationReport`;
- Unity Test Runner, filtered/full `EditMode` и `PlayMode`;
- `MSC.Editor.Foundation.FoundationValidationRunner.RunBatch`;
- `MSC.Editor.GaragePrototype.GaragePrototypeValidator.RunBatch`;
- `MSC.Editor.PlayerInteraction.PlayerInteractionPrototypeValidator.RunBatch`;
- `MSC.LegacyImport.Editor.Validation.DonorPipelineValidationRunner.RunBatch`;
- `MSC.Editor.ReferenceCapture.ReferenceCaptureValidationRunner.RunBatch`;
- `git status --short`, `git diff --check`, CSV parsing checks.

## Созданные и изменённые файлы

Основные новые группы:

- `Assets/Game/Vehicle/Assembly/Runtime/` — assembly domain/runtime и DTO;
- `Assets/Game/Editor/VehicleAssembly/` — builder, validator, graph window и performance audit;
- `Assets/Game/Vehicle/Content/Assembly/Definitions/` — 15 part, 14 mount, 14 fastener и 6 tool definitions;
- `Assets/Game/Vehicle/Content/Assembly/Prefabs/` — 15 clean prototype prefabs;
- `Assets/Game/Vehicle/Content/Assembly/Materials/` — prototype/preview materials;
- `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`;
- `Assets/Game/Tests/EditMode/VehicleAssembly/VehicleAssemblyEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/VehicleAssembly/VehicleAssemblyPlayModeTests.cs`;
- `Docs/Vehicle/` — architecture, authoring, mount/fastener guide, test matrix, limitations, validation и performance reports;
- `Docs/Milestones/MILESTONE_05_REPORT.md`.

Обновлены test asmdefs, Build Settings, architecture/current state, roadmap, testing, vehicle system, donor audit, porting matrix/system map/ledger. Существующие незакоммиченные M04B материалы сохранены.

## Ручная проверка в Unity

1. Откройте `Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`.
2. Нажмите Play.
3. Подойдите к rear-left brake drum и нажмите `E` для pickup.
4. Поднесите его к синему rear-left drum mount; ghost должен стать зелёным при валидном pose и красным при неверном.
5. Нажмите `E`: drum должен snap, стать installed/unfastened и перестать быть dynamic.
6. Наведитесь на жёлтый fastener target и нажимайте `R`: стадии идут `0 → 8`, затем `8 → 0`.
7. На стадии `> 0` снятие должно быть запрещено.
8. На стадии `0` нажмите `E` по drum: он снимается и снова становится pickup-ready.
9. Установите drum, затем rear-left wheel; попытка снять drum должна сообщить blocker.
10. Проверьте Console на отсутствие exceptions и visually оцените preview readability/collision jitter.

## Ограничения и риски

- 15 деталей — prototype coverage, не полный автомобиль и не production art.
- Большинство masses/mounts/fasteners/tolerances не donor-calibrated.
- Нет fluids, wiring, IK, animation, audio, wear/damage и detailed vehicle physics.
- Tool selection временно authored на fastener target; inventory отсутствует.
- Save DTO не подключён к disk backend.
- Linear mount scan проверен для 14 точек; масштабирование требует профилирования.
- Full EditMode остаётся красным из-за отдельного 04A1 donor hash drift.
- Требуется ручной Game View smoke test.

## Readiness

- Для первого bounded world-remaster integration pass: **GO**. Assembly имеет переносимую scene/prefab/data boundary и не зависит от donor content.
- Для Milestone 06 architecture: **foundation ready**, но simulation constants, wheel identity/mass calibration и driving fixtures остаются отдельными gates; M06 в этом milestone не начинался.

## Ровно один следующий milestone

**Milestone 05A — `Prompts/05A_WORLD_REMASTER.md`.**

Интегрировать завершённый assembly prototype в первый ограниченный remastered world pass, не начиная M06 vehicle simulation.
