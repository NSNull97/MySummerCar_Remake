# Milestone 05B.1 — сводка PilotGate remediation

Дата проверки: 2026-07-15
Unity: `6000.3.11f1`
Валидатор: `05B.1`

## Решение gate

Достигнутый уровень — **`PilotGate`**.

| Gate | Результат | Блокирующие finding IDs |
|---|---|---|
| `PilotGate` | **PASS** | нет |
| `VerticalSliceGate` | FAIL | `WORLD-BLD-001`, `WORLD-COL-001`, `WORLD-GEO-001`, `WORLD-GEO-002`, `WORLD-LOD-001`, `WORLD-PERF-002`, `WORLD-PERF-003`, `WORLD-ROAD-001`, `WORLD-STREAM-003`, `WORLD-STREAM-004`, `WORLD-STREAM-005` |
| `FullWorldGate` | FAIL | blockers Vertical Slice плюс `WORLD-DONOR-002`, `WORLD-GEO-004`, `WORLD-ID-002` |

Машиночитаемый источник: `Docs/WorldValidation/WORLD_VALIDATION_RESULT.json`.
Текущий результат не повышает bounded pilot до Vertical Slice или полного мира.

## Что закрыло PilotGate

- production streaming service подключён в `Bootstrap` через явный composition root;
- manifest содержит ровно `cell_0_-3` и `cell_0_-2`, cell size `512 m`, load radius `0`, unload radius `1`;
- production cells адресуются только по build index `6` и `8` с проверкой точного scene path;
- service выгружает только сцены, которые загрузил сам;
- fingerprinted PlayMode evidence подтверждает два цикла `pilot -> both -> next -> none`, owned counts `1,2,1,0`, уничтожение roots и уникальность stable IDs;
- настоящий M4 `FirstPersonMotor` + `CharacterController` прошёл 16 checkpoints через garage, house portal, representative interior и обратно;
- route evidence привязано к recursive asset + `.meta` dependency closure, production cell, player prefab и relevant scripts;
- принят bounded Windows x64 Development Player performance capture в четырёх доступных точках.

## Покрытие и spatial parity

Показатели покрытия не изменились, поскольку 05B.1 не добавлял broad world art:

- direct bindings: **33/13 509 = 0.244282%** canonical inventory;
- eligible reference-world bindings: **33/3 842 = 0.858928%**;
- production-bound cells: **2/49 = 4.081633%**;
- `cell_0_-3`: **24/671 = 3.576751%**;
- `cell_0_-2`: **9/15 = 60.000000%**;
- Approved/Verified replacements: **0/3 842**;
- 05C representation: 2 784 actual meshes и 1 058 explicit bounds fallbacks;
- 05C1 safety topology: 2/2 project-owned pieces.

Доступный spatial fixture set также не менялся:

- maximum: **0.232306 m**;
- mean: **0.051240 m**;
- nearest-rank p95: **0.232306 m**;
- home/pier overlap: `3.75 m`;
- pier collider gap: `0.08 m` при limit `0.10 m`;
- 05C1 seam: 26 пар с `0 m` deviation;
- 05C1 collision probes: `100/100`.

Full terrain, road graph, junctions, shoreline elevation, building footprints и
полный interior floor-level parity остаются недоступны и не подменены нулями.

## Production independence

Свежий dependency graph: **50 seed assets / 235 visited / 489 edges / 0 forbidden dependencies**.
Запрещённых production ссылок на `ReferenceOnly`, `DonorGenerated` и project Editor content нет.
Enabled Build Settings order теперь экспортируется без алфавитной перестановки;
pilot/next production cells действительно имеют indices `6/8`.

Development Player текущего мира был собран для performance capture, но его
`ScriptingAssemblies.json` отдельно не аудирован. Поэтому `WORLD-DONOR-002`
остаётся открытым только для `FullWorldGate`.

## Traversal

Fingerprint evidence:

- route fingerprint: `f8c915039e48f3c5f8f8fe1a2a8f75b84bca614c01f8505720a69cd402e2e1da`;
- dependency fingerprint: `719fdc2621c83c3d4c261adee62f95ed5db4ee28e7b827fc39041a83f5da66bf`;
- 16/16 checkpoints;
- cumulative horizontal distance: `62.780293 m`;
- max per-frame horizontal displacement: `0.100586 m`;
- max waypoint vertical deviation: `0.26 m` при contract ceiling `0.35 m`;
- route extent: `17.0 x 10.4 m`;
- no teleport, stall guard, grounded vertical reach, crouch passage и return-to-start: PASS.

Низкий `FrontDoorHeader` требует crouch; standing clearance не заявляется.
Fixture напрямую открывает три authored doors и отключает `PlayerInputRouter`,
поэтому это проверка реального motor/controller и collision route, а не полный
пользовательский input/interaction walkthrough. Streaming lifecycle и traversal
остаются двумя дополняющими fixtures, а не единым streamed end-to-end route.

## Performance

Windows x64 Development Player, `1920x1080`, HDRP `High Fidelity`, D3D12,
Ryzen 9 5950X, RTX 4070 SUPER:

| Location | Frame mean / p95 / worst, ms | CPU p95, ms | GPU p95, ms | Draw / Batches / SetPass mean |
|---|---:|---:|---:|---:|
| `pilot-home` | `3.014 / 3.620 / 3.894` | `3.430` | `3.329` | `225.97 / 168.96 / 26.98` |
| `dense-vegetation` | `3.223 / 3.863 / 4.266` | `3.669` | `3.475` | `223.03 / 193.02 / 27.99` |
| `interior-transition` | `3.296 / 4.003 / 4.345` | `3.737` | `3.444` | `231.97 / 214.95 / 30.99` |
| `water-shoreline` | `3.131 / 3.803 / 4.001` | `3.570` | `3.442` | `57.00 / 45.00 / 28.00` |

Четыре verification PNG имеют уникальные SHA-256 и sampled signatures.
Production exposure defect исправлен: `BootstrapGlobalVolume` и generator теперь
используют fixed EV100 `14` при солнце `100000 lux`.

`WORLD-PERF-001` закрыт. `WORLD-PERF-002/003` открыты: отсутствуют resident VRAM,
isolated Present, physics counter, чистый streaming-hitch capture, road-at-speed и
полный vertical-slice route. Эти bounded цифры не являются гарантией финальных 60 FPS.

## Findings

Всего: **27**, закрыто **12**, открыто **15**.

Открыты:

`WORLD-BLD-001`, `WORLD-COL-001`, `WORLD-COL-003`, `WORLD-DONOR-002`,
`WORLD-GEO-001`, `WORLD-GEO-002`, `WORLD-GEO-004`, `WORLD-ID-002`,
`WORLD-LOD-001`, `WORLD-PERF-002`, `WORLD-PERF-003`, `WORLD-ROAD-001`,
`WORLD-STREAM-003`, `WORLD-STREAM-004`, `WORLD-STREAM-005`.

Закрыты в текущем baseline:

`WORLD-COL-002`, `WORLD-COL-004`, `WORLD-DONOR-001`, `WORLD-GEO-003`,
`WORLD-GEO-005`, `WORLD-GEO-006`, `WORLD-GEO-007`, `WORLD-ID-001`,
`WORLD-ID-003`, `WORLD-PERF-001`, `WORLD-STREAM-001`, `WORLD-STREAM-002`.

## Проверки

- strict streaming validator: PASS, `errors=0`, `warnings=0`;
- streaming lifecycle PlayMode evidence: `1/1 PASS`, два цикла;
- traversal PlayMode evidence: `1/1 PASS`;
- focused WorldValidation EditMode: `12/12 PASS`;
- full PlayMode: `27/27 PASS`;
- full EditMode: `139/142 PASS`.

Три известные EditMode failures не созданы 05B.1:

- два M3 lighting assertions видят user-owned `M3_NeutralVolume.asset` со `skyType=1` вместо frozen contract `4`;
- WorldTransfer dry run фиксирует известный donor provenance drift для `sharedassets3.assets` и `sharedassets3.resource`.

## Go/no-go

`06_VEHICLE_SIMULATION.md`: **GO**, потому что `PilotGate` достигнут.

Ровно следующий milestone: **`06_VEHICLE_SIMULATION.md`**. 05B.1 не реализует и не начинает его.
