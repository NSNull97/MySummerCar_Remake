# Performance capture — Milestone 05B.1 bounded world pilot

Дата подготовки инфраструктуры: 2026-07-15

Статус: **bounded Windows x64 Development Player capture получен и проверен**.

Фактический capture выполнен 2026-07-15 на Unity `6000.3.11f1`, Windows 10,
Ryzen 9 5950X и RTX 4070 SUPER, Direct3D 12, quality `High Fidelity`, `1920x1080`.
Он подтверждает только bounded steady-state четырёх текущих точек и не является
гарантией 60 FPS полной будущей игры. Старый M3 garage capture не использовался
как evidence для текущего мира.

## Фактический результат

Player завершился с exit code `0`, log содержит
`M05B1_WORLD_PERFORMANCE_CAPTURE_OK`, JSON содержит `completed: true` и четыре
location records. До обновления этого отчёта strict provenance comparison дал:
Git revision match, `53` captured/current dirty entries и `0` mismatches.

| Location | Frame mean / p95 / worst, ms | CPU p95, ms | GPU p95, ms (samples) | Draw / Batches / SetPass mean |
|---|---:|---:|---:|---:|
| `pilot-home` | `3.014 / 3.620 / 3.894` | `3.430` | `3.329` (`239`) | `225.97 / 168.96 / 26.98` |
| `dense-vegetation` | `3.223 / 3.863 / 4.266` | `3.669` | `3.475` (`261`) | `223.03 / 193.02 / 27.99` |
| `interior-transition` | `3.296 / 4.003 / 4.345` | `3.737` | `3.444` (`284`) | `231.97 / 214.95 / 30.99` |
| `water-shoreline` | `3.131 / 3.803 / 4.001` | `3.570` | `3.442` (`259`) | `57.00 / 45.00 / 28.00` |

Steady-state p95 каждой точки ниже бюджета `16.667 ms`. Это предварительное
bounded evidence, а не утверждение о полном маршруте или финальном мире.

Все четыре PNG существуют, визуально содержат геометрию и имеют четыре разных
SHA-256 и четыре разных sampled signatures. Автопроверка зафиксировала:

| Location | RGB buckets | Dominant fraction | Luminance range | Signature |
|---|---:|---:|---:|---|
| `pilot-home` | `43` | `0.5786` | `0.4902` | `7a2e86a2a0060dcc` |
| `dense-vegetation` | `56` | `0.1775` | `0.4588` | `83b1a2ea83fa1733` |
| `interior-transition` | `7` | `0.9720` | `0.1725` | `52412486e8dcd226` |
| `water-shoreline` | `45` | `0.1739` | `0.3686` | `52bfaa562fd5a58f` |

Production-дефект, делавший кадр полностью белым, исправлен согласованно:
`FoundationSceneBuilder` и `BootstrapGlobalVolume` теперь используют fixed
exposure `14` при directional sun `100000 lux`; точный Foundation EditMode test
прошёл (`3/3`). User-owned `M3_NeutralVolume.asset` не изменялся этой работой.

## Границы проверки

Runtime-сценарий использует только существующий production streamer и две принятые
ячейки:

- `cell_0_-3` — home/pilot;
- `cell_0_-2` — shoreline/pier.

Build получает весь текущий список enabled scenes в неизменном порядке. Это не
расширение runtime scope: manifest адресует две production cells по enabled build
index, поэтому удаление промежуточных enabled scenes только для capture изменило бы
их индексы. Player стартует с `Assets/Game/Bootstrap/Bootstrap.unity` (index 0), а
остальные prototype scenes не загружаются capture-сценарием.

Перед сборкой выполняются строгие проверки:

1. `WorldPilotGateRemediationValidator` должен пройти;
2. manifest должен содержать ровно `cell_0_-3` и `cell_0_-2`;
3. каждый `BuildIndex` manifest обязан указывать на его точный `ScenePath` в
   текущем enabled порядке;
4. Bootstrap обязан оставаться index 0.

Probe добавляется только в транзитную build-копию Bootstrap через build callback.
Production scene на диске не получает profiling-компонент.

## Методика capture

- Windows x64 Development Player;
- запрошенный режим `1920x1080`, windowed;
- обычный player backbuffer, без M3 synchronous offscreen render request;
- VSync `0`, `Application.targetFrameRate = -1`;
- по 120 warmup и 300 measured frames в каждой точке;
- четыре доступные точки: `pilot-home`, `dense-vegetation`,
  `interior-transition`, `water-shoreline`;
- в каждой точке камера удерживается на проверяемой production-геометрии:
  `GarageDoorLeft`, ближайшей `Spruce_*`, `LivingTable` и `PierDeck` соответственно;
- при переносе focus из pilot в shoreline отдельно записываются длительность
  additive load transition и максимальный observed frame time;
- measured frames и render counters относятся к обычному player backbuffer;
- после measured frames для каждой точки HDRP выполняет один синхронный
  `RenderPipeline.StandardRequest` в отдельный `1920x1080 ARGB32 sRGB`
  `RenderTexture`; он используется только для проверочного PNG и не входит в
  300 измеренных кадров;
- Player должен запускаться в обычном видимом окне. Hidden/minimized запуск не
  принимается как performance evidence.

JSON содержит:

- ordinary frame time: mean, median, p95, p99, worst и расчётный mean FPS;
- доступные CPU/GPU `FrameTimingManager` samples;
- доступные `ProfilerRecorder` counters: total/reserved/GC memory, draw calls,
  batches, SetPass, main thread и physics processing;
- фактически загруженные scene build indices после каждого перемещения;
- requested/actual position и разрешение;
- явный список unavailable counters/metrics;
- Unity/OS/CPU/GPU/API/quality;
- Git commit, `clean`/`dirty` и repo-relative dirty paths на момент сборки;
- для каждого dirty entry — точный двухсимвольный porcelain status, repo-relative
  path и SHA-256 текущих байтов (`missing` только когда текущего файла нет,
  в частности при deleted status).

Сборка fail-fast останавливается, если Git provenance получить нельзя. При текущем
незакоммиченном baseline запись для user-owned
`Assets/Game/Presentation/Lighting/GaragePrototype/M3_NeutralVolume.asset` обязана
попасть в `sourceDirtyEntries` со status ` M` и 64-символьным SHA-256. Capture не
меняет этот asset и позволяет однозначно связать результат с его фактическими
байтами, не приписывая изменение Milestone 05B.1.

Нулевой или отсутствующий GPU/counter sample не трактуется как нулевая стоимость:
capture завершается с ошибкой, если GPU FrameTiming, draw calls, batches или
SetPass не дали положительных samples. PNG также проверяется на диапазон яркости,
цветовое разнообразие и уникальную sampled signature. Точные пороги: не менее
6 квантованных RGB-buckets, доля доминирующего bucket не выше `0.985`, диапазон
яркости не ниже `0.05`; все четыре sampled signatures обязаны различаться.
Одинаковые или почти одноцветные кадры не принимаются.

## Локальные ignored outputs

- `Builds/Milestone05B1/MySummerCar_Remake_M05B1_Performance.exe`;
- `PerformanceCaptures/Milestone05B1/WorldPilot_1080p.json`;
- `PerformanceCaptures/Milestone05B1/WorldPilot_1080p_<location>.png`;
- Unity player log в стандартном `%USERPROFILE%/AppData/LocalLow/...` либо в
  явно переданном `-logFile`.

Формальный контракт JSON:
`Docs/Performance/MILESTONE_05B1_PILOT_CAPTURE.schema.json`.

## Воспроизведение

Сначала собрать streaming baseline и проверить его, затем собрать player:

```text
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MSC.Editor.WorldStreaming.ProductionWorldStreamingBuilder.RunBatch -logFile <streaming-build-log>
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MSC.Editor.WorldStreaming.WorldPilotGateRemediationValidator.RunBatch -logFile <streaming-validation-log>
Unity.exe -batchmode -quit -projectPath <project> -executeMethod MSC.Editor.WorldPerformance.WorldPilotPerformanceBuild.RunBatch -logFile <performance-build-log>
```

Запуск capture:

```text
MySummerCar_Remake_M05B1_Performance.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -msc-05b1-capture <absolute-json-path> -logFile <absolute-player-log>
```

Успех подтверждается одновременно:

- exit code `0`;
- строкой `M05B1_WORLD_PERFORMANCE_CAPTURE_OK` в player log;
- `completed: true` в JSON;
- положительными GPU FrameTiming, Draw Calls, Batches и SetPass во всех точках;
- четырьмя location records и четырьмя различающимися, автоматически и визуально
  проверенными PNG.

## Ограничения

- `road-driving-speed` и полный `vertical-slice-route` отсутствуют как production
  content и намеренно не симулируются этим bounded capture;
- фиксируется ёмкость GPU, но не заявляется недоступный resident/allocated VRAM usage;
- измерение не выделяет отдельную стоимость Present;
- `loadTransitionMaximumFrameTimeMilliseconds` после первой точки считается
  diagnostic-only: следующий frame delta может включать синхронное создание
  предыдущего verification PNG. Эти значения не закрывают streaming-hitch
  validation;
- результат одной машины не является общим hardware target;
- p95 ниже `16.667 ms` записывается как предварительный индикатор, а не как
  доказательство стабильных 60 FPS будущей полной игры;
- interior verification PNG намеренно очень тёмный, но не одноцветный и проходит
  все зафиксированные пороги; lighting readability остаётся отдельной задачей;
- полный driving route и повторные captures на target hardware остаются будущей
  проверкой.
