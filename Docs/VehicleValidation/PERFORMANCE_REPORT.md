# Milestone 06A — отчёт производительности vehicle physics

Дата evidence: 2026-07-16  
Unity: `6000.3.11f1`  
Hardware: AMD Ryzen 9 5950X, Windows 10 x64, `Null Device`  
Статус bounded автоматического performance gate: **PASS**

## Что измерено

M06A содержит три раздельных контура:

1. `M06A_PHYSICS_VALIDATION_PERFORMANCE.json` — pure-managed Stopwatch audit `VehicleSimulationRoot` с `FixedPerformanceBackend`.
2. `production-world-transition.csv` — instrumented timings реального M06A PlayMode fixture на bounded production-маршруте.
3. `M06A_PHYSX_RUN_EVIDENCE.json` schema v4 — отдельное PlayMode-окно с реальным `PrototypeRaycastWheelPhysicsBackend`, ручным `VehicleSimulationRoot.Tick` и блокирующим `Physics.Simulate`.

Pure benchmark использует `512` warmup ticks и `20000` measured ticks. CSV-анализ production fixture отбрасывает первые `50` samples и анализирует следующие `356`. Scripted PhysX window использует `128` warmup ticks и три trials по `800` measured ticks в каждом режиме.

## Pure-managed benchmark

| Контур | Substeps | Время, µs/tick | Allocated, B/tick | Результат |
|---|---:|---:|---:|---|
| baseline simulation | 1 | `2.571855` | `0` | PASS |
| simulation | 2 | `3.552010` | `0` | PASS |
| active config | 4 | `5.962805` | `0` | PASS |
| telemetry snapshot | 4 | `6.048860` | `0` | PASS |
| scripted validation input | 4 | `6.952690` | `0` | PASS |
| scripted input + telemetry | 4 | `6.817740` | `0` | PASS |

Порог `2000 µs/tick` и `0.25 B/tick` является только regression sanity guard этого изолированного benchmark. CSV formatting и file I/O не входят в telemetry snapshot measurement.

## Production-world telemetry

После `50` warmup samples:

| Поле telemetry | Mean, ms | p95, ms | Max, ms |
|---|---:|---:|---:|
| `simulation_ms` | `0.038197` | `0.0461` | `0.0629` |
| `backend_sample_ms` | `0.020836` | `0.0261` | `0.0393` |
| `backend_apply_ms` | `0.006555` | `0.0088` | `0.0116` |

Для этой CSV-выборки p95 рассчитан линейной интерполяцией. Выборка подтверждает отсутствие заметного regression внутри измеряемых участков на текущей машине и текущем bounded fixture, но не является полным временем physics frame.

## Scripted PhysX performance

Оба режима используют реальный raycast backend, четыре project simulation substeps, статически поддерживаемый автомобиль в нейтрали и ручной default-physics-scene step. Это bounded stationary solver/backend sanity, а не дорожный benchmark.

| Режим | Root Tick mean, ms | `Physics.Simulate` mean, ms | Combined mean, ms | p95 combined, ms | Max combined, ms | Allocated, B/tick |
|---|---:|---:|---:|---:|---:|---:|
| telemetry consumer off | `0.013691` | `0.024990` | `0.038737` | `0.0534` | `0.2084` | `0` |
| telemetry consumer on | `0.013175` | `0.023025` | `0.036251` | `0.0432` | `0.1826` | `0` |

В каждом режиме измерено `2400` ticks, minimum contact count равен `4`, invalid state count равен `0`. `Physics.Processing` ProfilerRecorder оказался недоступен, но синхронный wall-clock вызова `Physics.Simulate` измерен напрямую.

## Неизмеренные границы

Следующее не измерено и не должно выводиться из приведённых чисел:

- Unity Profiler marker `Physics.Processing` как отдельный counter;
- детализация collision detection и solver cost внутри измеренного `Physics.Simulate`;
- GPU и rendering;
- полный world streaming cost;
- Windows x64 Player/Development build;
- общий frame time и подтверждение стабильных 60 FPS;
- custom-versus-simple backend comparison: второго backend нет, статус `N/A`.

Ни pure benchmark, ни telemetry CSV, ни stationary scripted PhysX window не дают права утверждать production performance parity или готовность полного мира.

## Артефакты и воспроизведение

- JSON: `Docs/VehicleValidation/M06A_PHYSICS_VALIDATION_PERFORMANCE.json`;
- PhysX JSON: `Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json`;
- production telemetry: `Docs/VehicleValidation/Telemetry/production-world-transition.csv`;
- Unity menu: `Tools > MSC Remake > Vehicle Physics Validation > Run Performance Audit`;
- batch log: `Logs/M06A_Performance.log`.

## Вердикт

- `PASS` для pure-managed regression sanity, instrumented bounded telemetry и stationary scripted PhysX sanity.
- `NO-GO` для заявления о полном physics CPU budget, GPU budget или 60 FPS.
- Перед production performance acceptance нужен Player build с Unity Profiler capture, включающим доступный physics marker, rendering, streaming и общий frame time.
