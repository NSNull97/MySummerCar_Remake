# Performance validation — Milestone 05B

Дата: 2026-07-15
Итог: **только статические метрики; runtime performance gate не закрыт**.

## Машина проверки

- CPU: AMD Ryzen 9 5950X 16-Core Processor;
- GPU: NVIDIA GeForce RTX 4070 SUPER;
- RAM: 31.9 GiB;
- OS: Windows 10 Home;
- Unity: 6000.3.11f1;
- режим текущей 05B проверки: Editor batch validation/tests.

Отдельный current-world Windows x64 build и performance capture не выполнялись.
Поэтому разрешение, HDRP quality preset, CPU/GPU frame time и frame pacing для
текущего world baseline отсутствуют.

## Доступные статические метрики

| Location | Status | Доступное evidence |
|---|---|---|
| pilot/home (`cell_0_-3`) | StaticOnly | 332 renderers, 134 colliders, 64 LODGroups, 158 548 triangles |
| dense vegetation | StaticOnly | 64 deterministic tree instances, два LOD stages |
| interior transition | StaticOnly | representative garage/living/kitchen/sauna slice присутствует |
| water/shoreline (`cell_0_-2`) | StaticOnly | 38 renderers, 30 colliders, 3 LODGroups |
| most expensive known production cell | StaticOnly | `cell_0_-3` плотнее единственной другой production-bound cell |
| road at driving speed | Unavailable | bounded road strip; production Road coverage 0/29 |
| vertical-slice route | Unavailable | непрерывного production route к service destination нет |

Свежий static audit двух production prefabs: **370 renderers, 164 colliders,
67 LODGroups, 159 548 instance-counted triangles**.

## Недоступные метрики

- CPU main/render/physics frame time;
- GPU и Present time;
- mean, p95 и p99 frame pacing;
- process/system memory, VRAM и managed allocations;
- draw calls, batches и SetPass calls;
- visible renderers/triangles по runtime camera;
- cell load/unload peak memory и recovery;
- worst-frame streaming spike;
- road-at-speed и full route capture.

Старый standalone M3 garage capture не используется как текущий 05B результат:
он не содержит интегрированную production map и не измеряет production streaming.

## Решение

Нельзя утверждать достижение 60 FPS или любого другого target. `WORLD-PERF-001`,
`WORLD-PERF-002` и `WORLD-PERF-003` остаются открыты и блокируют
`VerticalSliceGate`/`FullWorldGate`.

После подключения production streamer нужен один current-world development build
и воспроизводимый capture для доступных home, vegetation, interior и shoreline
locations, включая cell transition. Road-at-speed и vertical-slice route остаются
недоступными до появления соответствующего production content.
