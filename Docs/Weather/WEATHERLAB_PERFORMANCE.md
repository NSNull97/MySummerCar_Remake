# WeatherLab: performance capture plan

Дата среза: 2026-07-17.
Статус всех числовых performance captures: `PENDING`; refresh-specific case отложен, пока adapter намеренно не поддерживает эту capability.

Ни один числовой результат ниже не считается измеренным до появления development-build capture и raw evidence.

## Цель 07A

Изолированно измерить presentation cost Enviro в небольшой WeatherLab. Эти результаты не являются production-world budget и не подтверждают стабильные 60 FPS на полном donor baseline.

Целевой evidence root:

`E:\GAYmDev_Studio\MySummerCar_Remake\PerformanceCaptures\Milestone07A`

Предлагаемые файлы:

```text
E:\GAYmDev_Studio\MySummerCar_Remake\PerformanceCaptures\Milestone07A\M07A_WeatherLab_Performance.json
E:\GAYmDev_Studio\MySummerCar_Remake\PerformanceCaptures\Milestone07A\M07A_WeatherLab_Profiler.data
E:\GAYmDev_Studio\MySummerCar_Remake\PerformanceCaptures\Milestone07A\Captures\...
```

## Фиксированные условия

- Windows x64 development build, если builder доступен;
- 1920 x 1080;
- один и тот же exterior performance anchor `W07A-CAP-PERF-01`;
- одинаковый camera FOV и VSync/frame cap policy, записанный в evidence;
- минимум 10 секунд warm-up после state transition;
- capture window минимум 30 секунд;
- не выполнять reflection refresh внутри steady-state window;
- adapter получает один revision до capture и не отправляет unchanged frames;
- Enviro Audio disabled;
- donor baseline не загружен;
- hardware/driver/build commit/Unity version записаны в JSON.

Если development build невозможен, Editor capture допускается только как явно помеченный `EDITOR_DIAGNOSTIC`; он не заменяет development-build результат.

## Метрики

В рабочем дереве присутствует `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\Development\WeatherLab\Runtime\WeatherLabPerformanceProbe.cs`. Он собирает только sample count, average и max unscaled frame time (по умолчанию 600 samples). Это diagnostic smoke helper, не замена Profiler capture и не источник перечисленных ниже метрик.

Обязательные:

- median/P95 CPU frame time;
- main thread и render thread frame time;
- GPU frame time;
- FPS как производная, не основная метрика;
- total used memory и delta относительно baseline;
- GC allocated bytes/frame;
- draw calls/batches/triangles;
- shadows;
- volumetric clouds;
- Enviro fog/volumetrics;
- precipitation/effects;
- reflection probe refresh spike отдельно от steady state — только после будущего решения добавить bounded refresh capability;
- vegetation wind;
- adapter `Present` duration и calls count;
- active manager/volume/directional-light counts.

## Capture matrix

| Case ID | Presentation | Tier | Result |
|---|---|---|---|
| `W07A-PERF-DISABLED-LOW` | Enviro presentation disabled diagnostic baseline | Low | PENDING |
| `W07A-PERF-DISABLED-HIGH` | Enviro presentation disabled diagnostic baseline | High | PENDING |
| `W07A-PERF-CLEAR-LOW` | clear | Low | PENDING |
| `W07A-PERF-CLEAR-HIGH` | clear | High | PENDING |
| `W07A-PERF-OVERCAST-LOW` | overcast | Low | PENDING |
| `W07A-PERF-OVERCAST-HIGH` | overcast | High | PENDING |
| `W07A-PERF-RAIN-LOW` | rain | Low | PENDING |
| `W07A-PERF-RAIN-HIGH` | rain | High | PENDING |
| `W07A-PERF-STORM-LOW` | storm, no repeated lightning during steady-state | Low | PENDING |
| `W07A-PERF-STORM-HIGH` | storm, no repeated lightning during steady-state | High | PENDING |
| `W07A-PERF-FOG-LOW` | mist/fog | Low | PENDING |
| `W07A-PERF-FOG-HIGH` | mist/fog | High | PENDING |
| `W07A-PERF-NIGHT-LOW` | night/headlights | Low | PENDING |
| `W07A-PERF-NIGHT-HIGH` | night/headlights | High | PENDING |
| `W07A-PERF-REFLECTION-REFRESH` | explicit one-shot reflection refresh spike | High | DEFERRED_CAPABILITY_UNSUPPORTED |
| `W07A-PERF-LIGHTNING-REQUEST` | explicit one-shot visual lightning spike/cleanup | High | PENDING |

## Adapter overhead acceptance

Документальная цель, которую ещё требуется измерить:

- `Present` не вызывается каждый frame для неизменного state;
- steady state не создаёт adapter-managed GC allocations/frame;
- текущий adapter не выполняет refresh request; effective autonomous vendor reflection cadence требуется проверить отдельно;
- duplicate lightning sequence не создаёт новый effect;
- scene reload возвращает owner counts к единице без устойчивого memory growth.

Это критерии проверки, а не текущие PASS.

## Raw result template

Для каждого case сохраняются:

```text
caseId
capturedUtc
buildCommit
unityVersion
resolution
qualityTier
weatherBindingId
warmupSeconds
sampleSeconds
cpuMedianMs
cpuP95Ms
mainThreadMedianMs
renderThreadMedianMs
gpuMedianMs
memoryBytes
gcBytesPerFrame
drawCallsMedian
trianglesMedian
adapterPresentCalls
reflectionRefreshCalls
managerCount
volumeCount
directionalEnvironmentLightCount
notes
```

Пустые поля или Editor-only observations нельзя заполнять нулями: использовать `null`/`PENDING` и пояснение.

## Текущий результат

- Development build: не запускался для WeatherLab.
- Profiler capture: отсутствует.
- Low/high comparison: отсутствует.
- Adapter overhead: не измерен.
- Production 60 FPS readiness: не заявлена.
