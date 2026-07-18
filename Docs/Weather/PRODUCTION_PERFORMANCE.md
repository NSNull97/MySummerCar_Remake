# Production weather performance plan and evidence

Дата среза: 2026-07-17.

Статус: `ARCHITECTURAL_GUARDS_PRESENT / 07B EDITOR BASELINE AVAILABLE / 07C PRODUCTION SIGN-OFF PENDING`.

## Target

- Windows x64 Development Player;
- 1920×1080;
- stable 60 FPS initial target on mid-range gaming PC;
- active profile `donor-feature-parity-06b2`;
- route `W07C-PROD-ROUTE-01`.

Ни одно число Editor 640×480 не доказывает этот target.

## Existing evidence

`PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json` содержит
10-state Editor PlayMode baseline на D3D11 / RTX 4070 SUPER / 640×480. Он
полезен для regression, но не содержит надёжные Render Thread/GPU samples и не
измеряет production donor world.

Для 07C отдельный Development Player capture, profiler `.data` и production
route JSON пока отсутствуют. FPS, CPU/GPU budget и 60 FPS claim не заявляются.

## Implemented cost guards

- authoritative weather domain не зависит от числа loaded cells;
- один persistent manager/adapter вместо per-cell owners;
- presentation cadence — `0.25 s`;
- project-owned ambient/reflection request cadence — binding change, `600` game
  seconds или `25 m` listener movement; adapter coalescing cooldown — `1 s`;
- global wetness writes dirty-only;
- legacy wetness bridge кэшируется по scene events и использует 64-step
  quantization;
- per-slot MPB переиспользуется, `renderer.material` не вызывается;
- один Enviro-owned WindZone, без per-tree MonoBehaviour updates;
- runtime weather/lightning clones переиспользуются и очищаются;
- automatic storm ambient lightning ограничена spawn/load grace `45` game
  seconds и cooldown `90–240` game seconds;
- wet-road physics остаётся отключённой.

Это ограничения архитектуры, а не измеренный performance PASS.

## Production route

`W07C-PROD-ROUTE-01`:

1. Bootstrap home/garage spawn `(153.495, 1.1, -1028.03)`.
2. Yard и выезд на major road loop.
3. Несколько последовательных streaming boundaries.
4. Representative bridge.
5. Teimo/store и town context.
6. Возврат к home/garage.
7. Отдельная lake/shore station для fog/water/rain.

06B3 traversal через 24 cells подтверждает базовую streaming route, но не
заменяет weather-on performance capture.

## Required matrix

| Scenario | Low | Medium | High | Current status |
|---|---|---|---|---|
| Enviro-disabled diagnostic baseline | required | required | required | PENDING |
| Clear day | required | required | required | PENDING |
| Partly cloudy | required | required | required | PENDING |
| Overcast | required | required | required | PENDING |
| Drizzle/steady rain | required | required | required | PENDING |
| Heavy rain | required | required | required | PENDING |
| Fog/mist | required | required | required | PENDING |
| Storm/lightning | required | required | required | PENDING |
| Dawn/dusk/night | required | required | required | PENDING |
| Dense vegetation | required | required | required | PENDING |
| Interior looking out | required | required | required | PENDING |
| Water/shore | required | required | required | PENDING |
| Additive cell transition | required | required | required | PENDING |

## Required metrics

Для каждого scenario сохранить:

- frame mean/p95/p99;
- main/render thread;
- GPU frame time;
- GC allocations/frame;
- system/graphics memory;
- clouds/fog/precipitation/reflections/shadows;
- vegetation wind и wetness bridge;
- adapter/domain cost;
- cell load/unload spike;
- quality switch spike;
- manager/renderer/material counts before/after route.

Unavailable counter отмечается `UNAVAILABLE`, а не нулём.

## Acceptance

Production performance можно принять только после Development Player route и
сравнения Enviro-disabled/Low/Medium/High при неизменном world quality. Нельзя
скрывать regression снижением terrain, streaming, shadows или LOD вне явно
выбранного tier.

Текущий итог: `PENDING`; quality bindings существуют, но ещё не measured/coherent
по production cost.
