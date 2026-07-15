# Milestone 05B.1 — PilotGate remediation report

Дата: 2026-07-15
Unity: `6000.3.11f1`
Итог: **`PilotGate` достигнут**.

## Scope

Выполнен только bounded remediation, назначенный отчётом 05B:

1. подключить production streaming двух уже принятых cells;
2. доказать representative traversal настоящим M4 `CharacterController`;
3. получить current-world bounded performance capture;
4. повторно вычислить 05B gate.

Broad world geometry, Player/Interaction architecture и Milestone 06 не
расширялись. Исторический `MILESTONE_05B_REPORT.md` сохранён как исходный
`None/NO-GO` результат; этот файл фиксирует последующий remediation.

## Что было проверено

- `AGENTS.md` и требования `Prompts/05B_WORLD_VALIDATION.md`;
- текущие Bootstrap, Player M4, Interaction и production world implementations;
- Build Settings и две production cell scenes;
- world registry/status, machine validation result и stable issue IDs;
- существующие collision, spatial, coverage и production dependency fixtures;
- пользовательский dirty `M3_NeutralVolume.asset` как внешний baseline, без его изменения.

## Реализовано

### Production streaming

- manifest: ровно `cell_0_-3`/`cell_0_-2`, `512 m`, radii `0/1`, build indices `6/8`;
- Bootstrap installer с явными manifest, M4 prefab и focus references;
- load только по build index с точной проверкой scene path;
- hysteresis и owned-scene unload;
- no runtime name lookup;
- strict validator учитывает `EditorOnly` по всей parent chain;
- двухцикловый fingerprinted lifecycle evidence.

### M4 traversal

- сериализованный route из 16 checkpoints;
- реальный `FirstPersonMotor` + `CharacterController`, no teleport/stall;
- garage, yard, house step, crouched front-door portal, representative interior и return;
- pinned route fingerprint, distance/extent lower bounds и vertical checks;
- recursive dependency fingerprint включает production cell/prefabs/materials,
  player, relevant scripts, asset payload и `.meta` importer/GUID state;
- stale JSON удаляется до теста и принимается runner только после fresh pass.

Финальные значения:

- route fingerprint: `f8c915039e48f3c5f8f8fe1a2a8f75b84bca614c01f8505720a69cd402e2e1da`;
- dependency fingerprint: `719fdc2621c83c3d4c261adee62f95ed5db4ee28e7b827fc39041a83f5da66bf`;
- cumulative distance: `62.780293 m`;
- max frame displacement: `0.100586 m`;
- max vertical deviation: `0.26 m`;
- extent: `17.0 x 10.4 m`.

### Performance capture

- Windows x64 Development Player, 1920x1080, D3D12, HDRP High Fidelity;
- 120 warmup + 300 measured backbuffer frames в четырёх locations;
- positive GPU FrameTiming, Draw Calls, Batches и SetPass;
- четыре различающихся HDRP verification PNG;
- strict capture-source provenance `53/53`, mismatch `0` в момент принятия;
- durable accepted evidence с pinned raw/log/PNG hashes.

Frame p95: `3.620 / 3.863 / 4.003 / 3.803 ms` для pilot, vegetation,
interior и shoreline. Это bounded steady-state evidence одной машины, не
гарантия финальных 60 FPS.

Capture выявил production white-clipping defect: Bootstrap profile создавался с
EV100 `0` при солнце `100000 lux`. `FoundationSceneBuilder` и
`BootstrapGlobalVolume` синхронно исправлены на verified EV100 `14`; точный
Foundation test добавлен. User-owned M3 profile не менялся.

### Validation metadata

- pilot status синхронизирован на
  `DoorGatePass;LightingReadabilityLow;M4TraversalPass;PerformancePending`;
- registry/CSV регенерированы без пересборки production scene geometry;
- dependency audit сохраняет фактический Build Settings order;
- performance locations имеют `MeasuredBounded`, а не stale `StaticOnly`;
- `WORLD-PERF-001`, `WORLD-COL-002`, `WORLD-STREAM-001/002` закрыты;
- canonical result: `PilotGate`, 12 Closed / 15 Open.

## Основные созданные файлы

- `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs`;
- `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`;
- `Assets/Game/Editor/WorldStreaming/ProductionWorldStreamingBuilder.cs`;
- `Assets/Game/Editor/WorldStreaming/WorldPilotGateRemediationValidator.cs`;
- `Assets/Game/Development/Performance/WorldPilotPerformanceProbe.cs`;
- `Assets/Game/Editor/WorldPerformance/WorldPilotPerformanceBuild.cs`;
- `Assets/Game/World/Production/Runtime/WorldPilotTraversalRoute.cs`;
- `Assets/Game/World/Editor/WorldPilotTraversalAuthoring.cs`;
- `Assets/Game/World/Editor/WorldPilotDependencyFingerprintUtility.cs`;
- `Assets/Game/World/Editor/WorldPilotTraversalEvidenceReader.cs`;
- `Assets/Game/World/Editor/ProductionWorldStreamingLifecycleEvidenceReader.cs`;
- `Assets/Game/World/Editor/WorldPilotPerformanceEvidenceReader.cs`;
- `Assets/Game/Tests/EditMode/WorldRemaster/ProductionWorldStreamingEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldRemaster/WorldPilotTraversalPlayModeTests.cs`;
- `Docs/WorldValidation/M05B1_PRODUCTION_STREAMING_LIFECYCLE.json`;
- `Docs/WorldValidation/M05B1_M4_CHARACTER_CONTROLLER_TRAVERSAL.json`;
- `Docs/WorldValidation/M05B1_WORLD_PERFORMANCE_EVIDENCE.json`;
- `Docs/Performance/MILESTONE_05B1_PILOT_CAPTURE.md`;
- `Docs/Performance/MILESTONE_05B1_PILOT_CAPTURE.schema.json`;
- `Docs/Milestones/MILESTONE_05B1_REPORT.md`.

## Основные изменённые файлы

- `Assets/Game/Bootstrap/Bootstrap.unity`;
- `Assets/Game/Bootstrap/GameServiceBindings.cs`;
- `Assets/Game/Editor/Foundation/FoundationSceneBuilder.cs`;
- `Assets/Game/Presentation/Lighting/BootstrapGlobalVolume.asset`;
- `Assets/Game/Tests/EditMode/Foundation/FoundationContentTests.cs`;
- `Assets/Game/Tests/EditMode/Foundation/GameServiceBindingsTests.cs`;
- `Assets/Game/Tests/EditMode/WorldRemaster/WorldRemasterEditModeTests.cs`;
- `Assets/Game/Tests/EditMode/WorldRemaster/WorldValidationEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldRemaster/WorldRemasterPlayModeTests.cs`;
- `Assets/Game/World/Editor/ProductionWorldCellBuilder.cs`;
- `Assets/Game/World/Editor/WorldRemasterRegistryBuilder.cs`;
- `Assets/Game/World/Editor/WorldValidationRunner.cs`;
- `Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity`;
- `Assets/Game/World/Authoring/ReplacementProfiles/WR_WorldProductionAssetRegistry.asset`;
- `Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`;
- `Docs/WorldValidation/WORLD_VALIDATION_RESULT.json`;
- `Docs/WorldValidation/WORLD_VALIDATION_ISSUES.csv`;
- `Docs/WorldValidation/WORLD_VALIDATION_SUMMARY.md`;
- `Docs/WorldValidation/STREAMING_VALIDATION.md`;
- `Docs/WorldValidation/TRAVERSAL_AND_CLEARANCE.md`;
- `Docs/WorldValidation/PERFORMANCE_VALIDATION.md`;
- `Docs/WorldValidation/PRODUCTION_DEPENDENCY_AUDIT.md`.

## Выполненные проверки

| Проверка | Результат |
|---|---|
| Foundation focused EditMode | `3/3 PASS` |
| strict streaming validator | PASS, `0 errors / 0 warnings` |
| streaming lifecycle PlayMode | `1/1 PASS`, 2 cycles |
| M4 traversal PlayMode | `1/1 PASS` |
| focused WorldValidation EditMode | `12/12 PASS` |
| full PlayMode | `27/27 PASS` |
| full EditMode | `139/142 PASS` |
| Windows x64 performance build/player | build PASS, player `exit 0`, 4/4 locations |
| canonical validation export | `PilotGate`, 12 Closed / 15 Open |

Три полных EditMode failure являются известным внешним baseline:

1. M3 neutral profile `skyType=1` против frozen expected `4`;
2. M3 validator сообщает тот же incomplete restrained profile;
3. donor `sharedassets3.assets/resource` hashes отличаются от frozen provenance.

Ни одно из них не маскировалось и не приписывалось 05B.1.

## Ручные действия

Обязательных ручных действий для подтверждения `PilotGate` нет: gate основан на
fresh machine evidence. Необязательный smoke test:

1. открыть `Assets/Game/Bootstrap/Bootstrap.unity`;
2. запустить Play Mode;
3. проверить появление M4 player и загрузку home cell;
4. пройти garage/house portal crouched и убедиться, что переход focus к shoreline
   загружает next cell, а owned previous cell выгружается по hysteresis.

## Ограничения и риски

- `FrontDoorHeader` требует crouch; standing clearance не подтверждён;
- traversal fixture не проверяет полный input/interaction flow;
- streaming и traversal пока не единый end-to-end fixture;
- production road/service route отсутствует;
- coverage остаётся 33/3842 eligible records и 2/49 cells;
- performance не содержит resident VRAM, isolated Present, physics counter и
  валидированный streaming hitch;
- current-world `ScriptingAssemblies.json` отдельно не аудирован;
- user-owned `M3_NeutralVolume.asset` остаётся dirty и намеренно не изменён.

## Следующий milestone

**`06_VEHICLE_SIMULATION.md` — GO.**

Это ровно один следующий этап. 05B.1 завершён и не переходит к его реализации.
