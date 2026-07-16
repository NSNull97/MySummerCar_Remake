# Milestone 06A — план калибровки и проверки физики

Дата baseline: 2026-07-16  
Unity: `6000.3.11f1`  
Назначение: проверить стабильность M06 как проектного прототипа и не выдавать его за точную donor-калибровку.

Статус автоматического gate: **PASS**. Builder и validator прошли, focused EditMode — `7/7`, focused PlayMode — `8/8`. Пользователь записал ручное принятие bounded prototype baseline 2026-07-16; итоговый статус M06A — `Accepted / HumanAccepted`. Это не заменяет будущую donor-калибровку или production performance acceptance.

## Границы доказательств

Доступные donor/reference данные дают точные трансформы корней колёс, AABB кузовного mesh и сериализованную массу отдельного donor Rigidbody. Они не дают массу полностью собранной машины, центр масс, параметры установленной шины или контролируемые динамические характеристики.

Используются следующие классификации:

- wheel-root transforms и body-mesh AABB — `MeasuredDonorReference`;
- wheelbase и track width, вычисленные из wheel-root transforms, — `DerivedReference`;
- кандидат радиуса `tire_stock` — `PlausibilityTarget` со статусом `NeedsReview`;
- текущие mass, CoM, engine, drivetrain, brake, steering, suspension и surface response — `RemakeDesignTarget` / `ProvisionalProjectTuning`;
- отсутствующие donor acceleration, braking, coast-down, steering, suspension, clutch, ratios и torque targets — `Unknown`.

Ручная оценка прототипа M06 не становится `ObservedDonorReference`: это наблюдение remake-прототипа, а не оригинальной игры.

## Повторяемая конфигурация

`VehicleCalibrationProfile` фиксирует:

- `M06_VehicleSimulationConfig.asset` и версию tuning schema;
- логически установленные детали и их статус `ExcludedFromPhysicsCalibration`;
- массу Rigidbody proxy, явный центр масс, collider envelope и damping;
- wheel radius и semantic surface responses;
- погоду `Clear/Dry`, wetness `0`;
- fuel `15 L`, payload `0 kg`, cargo `0 kg`;
- `ScriptedVehicleInputSource`;
- fixed timestep `0.02 s`;
- выбранные `4` substeps чистой simulation-логики;
- Unity/PhysX boundary, режим запуска, hardware и seed;
- список fixtures, metrics и reference targets.

Сумма логических масс M06 (`871.7 kg`) не формирует массу Rigidbody proxy (`650 kg`) и отдельно помечена как исключённая из физической калибровки.

## Среды проверки

### Изолированный полигон

Сцена `Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity` создаётся отдельно от production world и не добавляется в Build Settings. Typed route registry содержит:

| Участок | Назначение |
|---|---|
| flat acceleration | разгон на прямой |
| braking | торможение и монотонность скорости |
| steering/slalom | steering step и ограниченная lateral response |
| suspension bump | препятствие и восстановление подвески |
| surface comparison | Paved, Gravel, Dirt, Grass |
| slope | проектный уклон `6°` |
| garage clearance | проём `3.12 × 2.22 m`, threshold `0.105 m` |
| collision transition | стык соседних colliders без намеренного зазора |

Полигон не изменяет production layout.

### Bounded production fixture

Через `Assets/Game/Bootstrap/Bootstrap.unity` проверяется только технический маршрут дома:

- старт около `(153.495, 1.665734, -1036.23)`;
- открытые створки гаража и ворота driveway;
- выезд к streaming boundary `z = -1024`;
- автоматическая загрузка `Production_cell_0_-2` из исходной `Production_cell_0_-3`;
- сохранение четырёх wheel contacts при пересечении границы;
- отдельный next-cell-only contact probe на `z = -970`, где colliders предыдущей ячейки отключены;
- semantic surface lookup для garage, driveway, road и terrain.

Эти две production pilot cells не приняты как donor-spatial или visual parity. По ручной оценке пользователя от 2026-07-16 они не похожи на соответствующие места оригинальной игры и применяются в 06A только как технические collision/streaming fixtures.

## Автоматические запуски

| Fixture | Режим | Повторы | Фактический результат |
|---|---:|---:|---|
| start/idle | pure + PlayMode | 5 / 3 | старт `0.300 s`; idle mean `902.531 RPM`; max deviation `11.268 RPM`; finite state |
| acceleration | PlayMode PhysX | 3 | launch peak `1.0123 m/s`; distance `0.4446 m`; четыре контакта |
| braking | PlayMode PhysX | 3 | скорость `1.0123 → 0.1556 m/s`; distance `0.05417 m`; недопустимого роста скорости нет |
| coast-down | PlayMode PhysX | 3 | потеря `0.13915 m/s` за `3.0 s`; finite state; четыре контакта |
| steering/slalom | PlayMode PhysX | 1 bounded run | max yaw `38.037°`; lateral displacement `3.2538 m`; четыре контакта |
| suspension bump | PlayMode PhysX | 1 | peak compression `0.830184`; recovery `0.316295`; четыре контакта |
| surface comparison | PlayMode PhysX | 4 surfaces | typed lookup PASS; coefficients ordered; динамические coast-down losses сохранены как informational |
| hill start | PlayMode PhysX | 1 | уклон `6°`; drift `0.05179 m`; uphill progress `3.03695 m`; четыре контакта |
| garage/world transition | PlayMode PhysX | 1 bounded run | `302` route frames; progress `12.255066 m`; peak `2.489053 m/s`; boundary crossed; соседняя cell загружена; четыре контакта |

Повторяемость PhysX оценивается допусками и статистикой, а не как битовая детерминированность.

Для устойчивого scripted input тестовый протокол выдерживает `2 s` после запуска до замера idle и использует адаптивное scripted clutch engagement. Это стабилизация измерительного протокола, а не изменение числовой physics-конфигурации.

## Обязательные инварианты

- ни один проверенный state не содержит `NaN` или `Infinity`;
- RPM/speed relation по передачам соответствует текущим ratios, final drive и wheel radius;
- repeated trials остаются в заданном tolerance;
- при торможении нет недопустимого роста скорости;
- после bump отсутствует взрывная сила или неустойчивое раскачивание;
- surface coefficients имеют явный project-owned ordering;
- центр масс, collider и damping берутся из единого config, а не из скрытых builder literals;
- garage fixture и streaming boundary воспроизводимы;
- отсутствующие donor targets остаются пустыми/`Unknown`.

Фактический экспорт содержит `64 Passed`, `51 Informational` и `30 Unknown` metric results. `Unknown` не считается провалом запуска: это явно отсутствующее reference evidence, которое запрещено подменять проектным числом.

## Performance matrix

Editor Stopwatch audit измеряет:

- pure simulation при `1`, `2` и выбранных `4` substeps;
- telemetry snapshot off/on;
- scripted validation off/on;
- managed allocations за measured window.

Результаты pure benchmark при `1/2/4` substeps: `2.571855 / 3.552010 / 5.962805 µs/tick`; telemetry snapshot — `6.048860 µs/tick`; scripted validation — `6.952690 µs/tick`; measured allocations — `0 B/tick`.

Отдельная telemetry выборка production-world fixture после `50` warmup samples содержит `356` measured samples: `simulation_ms` mean `0.038197 ms`, linear p95 `0.0461 ms`, max `0.0629 ms`; backend sample mean `0.020836 ms`; backend apply mean `0.006555 ms`.

Восьмой PlayMode-тест выполняет реальный `PrototypeRaycastWheelPhysicsBackend` в ручном fixed-step цикле `VehicleSimulationRoot.Tick` + блокирующий `Physics.Simulate`. При выключенном telemetry consumer средние времена составили `0.038737 ms` combined и `0.024990 ms` внутри `Physics.Simulate`; при включённом — `0.036251 ms` и `0.023025 ms`. Оба режима выполнили по `2400` measured ticks, сохранили минимум четыре контакта, `0` invalid states и `0 B/tick`.

Profiler marker `Physics.Processing` в batch run недоступен, хотя полное wall-clock время блокирующего `Physics.Simulate` измерено. GPU, rendering, streaming cost целиком, Player build и общий frame time не измерены, поэтому evidence не является доказательством 60 FPS. Сравнение custom/simple backend имеет статус `N/A`, потому что в проекте существует только один `PrototypeRaycastWheelPhysicsBackend`.

## Инструменты

Меню Unity: `Tools > MSC Remake > Vehicle Physics Validation`.

- `Build / Rebuild Validation Setup` — миграция config, профиль, полигон и scene rig;
- `Validate` — статическая проверка assets, route, build settings и provenance;
- `Run Performance Audit` — JSON performance evidence;
- `Export Summary CSV` — targets, runs, metric results и tuning log;
- `Dashboard` — единая точка доступа и обзор fixtures.

## Durable evidence

- `CALIBRATION_TARGETS.csv`;
- `CALIBRATION_RUNS.csv`;
- `METRIC_RESULTS.csv`;
- `TUNING_CHANGE_LOG.csv`;
- `M06A_PHYSX_RUN_EVIDENCE.json` schema v4;
- `M06A_PHYSICS_VALIDATION_PERFORMANCE.json`;
- Unity Test Framework XML и batch logs в `TestResults/` и `Logs/`.

Числовая настройка physics baseline в 06A не выполняется без валидного reference target. Разрешённые изменения ограничены устранением скрытых tuning values, воспроизводимостью config и semantic surface metadata, прямо нужной для проверки.

## Gate и дальнейший маршрут

- Автоматический M06A gate: `PASS`.
- Текущий статус: `Accepted / HumanAccepted`.
- `GO` для перехода к bounded Milestone 06B; Milestone 07 пока остаётся за границей.
- `NO-GO` для заявлений о donor-world parity и полной production vehicle realism.
- Ровно следующий milestone: `06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE`.
