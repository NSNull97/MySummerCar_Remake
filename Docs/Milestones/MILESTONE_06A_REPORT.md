# Milestone 06A — Vehicle Physics Calibration and Validation

Дата: 2026-07-16  
Unity: `6000.3.11f1`  
Автоматический gate: **PASS**  
Acceptance status: **Accepted / HumanAccepted 2026-07-16**

## 1. Итог

Создан и выполнен воспроизводимый M06A validation gate для текущего M06 vehicle prototype. Builder и validator прошли, focused EditMode — `7/7`, focused PlayMode — `8/8`. Экспортированы profile-based targets, runs, metric results, tuning log, два JSON evidence и семь telemetry CSV.

Числовая physics-конфигурация M06 не менялась: доступные donor/reference данные не дают честного основания для динамической donor-калибровки. Milestone проверяет устойчивость project baseline и явно отделяет `MeasuredDonorReference`, `DerivedReference`, `PlausibilityTarget`, `RemakeDesignTarget` и `Unknown`.

## 2. Validation setup

- profile: `Assets/Game/Vehicle/Content/Validation/Resources/M06A_VehicleCalibrationProfile.asset`;
- isolated scene: `Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity`;
- runtime framework: `Assets/Game/Vehicle/Simulation/Validation/`;
- Editor tools: `Assets/Game/Editor/VehicleValidation/`;
- menu: `Tools > MSC Remake > Vehicle Physics Validation`;
- fixed timestep: `0.02 s`;
- selected simulation substeps: `4`;
- environment: `Clear/Dry`, wetness `0`, weather integration unavailable;
- input: repeatable scripted driver;
- fixtures: `11`;
- declared metrics: `50`.

Тестовый driver получил `2 s` idle settle before measurement и adaptive scripted clutch engagement. Это стабилизация протокола, а не изменение physics tuning.

## 3. Reference targets

Подтверждённые reference-derived значения ограничены geometry:

| Метрика | Значение | Классификация |
|---|---:|---|
| wheelbase | `2.334 m` | `DerivedReference` |
| front track | `1.260 m` | `DerivedReference` |
| rear track | `1.206 m` | `DerivedReference` |
| tire candidate radius | `0.272667 m` | `PlausibilityTarget / NeedsReview` |
| body mesh AABB | `1.465916 × 1.110294 × 3.546204 m` | `MeasuredDonorReference` |

Масса proxy `650 kg`, CoM `(0, 0.2, 0)`, drivetrain, steering, suspension и surface coefficients остаются provisional `RemakeDesignTarget`. Donor dynamic targets для acceleration, braking, coast-down, steering, suspension и top speed отсутствуют и сохранены как `Unknown`.

## 4. Выполненные метрики

Основные PhysX результаты:

| Fixture | Результат |
|---|---|
| start/idle | start `0.300 s`; idle mean `902.531 RPM`; max deviation `11.268 RPM` |
| launch | peak `1.0123 m/s`; distance `0.4446 m` |
| braking | `1.0123 → 0.1556 m/s`; distance `0.05417 m`; speed increase `0` |
| coast-down | loss `0.13915 m/s` за `3.0 s` |
| steering | max yaw `38.037°`; lateral displacement `3.2538 m`; progress `10.4731 m` |
| bump | baseline `0.316295`; peak compression `0.830184`; recovered `0.316295` |
| hill start | `6°`; drift `0.05179 m`; uphill progress `3.03695 m` |
| contacts | четыре контакта в проверенных bounded runs; invalid numeric state не обнаружен |

Surface lookup для `Paved`, `Gravel`, `Dirt`, `Grass` прошёл. Dynamic coast-down losses `0.320246 / 0.321290 / 0.319743 / 0.313770 m/s` не образуют строгий ordering и поэтому остаются informational.

Итоговый `METRIC_RESULTS.csv`: `64 Passed`, `51 Informational`, `30 Unknown`.

## 5. Before/after и tuning changes

| Change | Before | After |
|---|---|---|
| builder defaults | rebuild мог сбросить значения | create-only + schema migration |
| CoM | implicit collider-derived | central config `(0, 0.2, 0)`, численно без изменения |
| chassis collider | builder literals | central config, числа без изменения |
| Rigidbody damping | builder literals | central config, числа без изменения |
| home surface metadata | `Unknown` | garage `Paved`, driveway/road `Gravel`, terrain/shore approach `Grass` |
| numeric physics tuning | M06 provisional baseline | без изменений |

Полная запись находится в `Docs/VehicleValidation/TUNING_CHANGE_LOG.csv`. Test-protocol stabilization не записывается как tuning автомобиля.

## 6. World integration

Bounded production run стартует около `(153.495, 1.665734, -1036.23)`, открывает garage doors и driveway gates и пересекает streaming boundary `z = -1024`.

- route frames `302`;
- progress `12.255066 m`;
- reached `z = -1023.974915`;
- peak speed `2.489053 m/s`;
- max lateral deviation `0.213638 m`;
- minimum contacts `4`;
- zero-contact streak `0`;
- `cell_0_-2` загружена автоматически;
- post-streaming vertical delta `0.000231 m`.

Отдельный next-cell-only probe на `z = -970`, с отключёнными colliders `cell_0_-3`, сохранил `4` контакта и max vertical delta `0.000231 m`. Это подтверждает bounded contact support следующей cell, но не непрерывный drive от boundary до probe и не full-world continuity.

Обе production pilot cells остаются отклонёнными по donor spatial/visual fidelity и используются только как технические fixtures.

## 7. Проверки

| Проверка | Результат | Evidence |
|---|---|---|
| M06A builder | PASS | `Logs/M06A_Build.log` |
| M06A validator | PASS | `Logs/M06A_Validate.log` |
| focused EditMode | `7/7 PASS` | `TestResults/M06A_EditMode.xml` |
| focused PlayMode | `8/8 PASS` | `TestResults/M06A_PlayMode.xml` |
| performance audit | PASS в bounded scope | `Logs/M06A_Performance.log` |
| evidence export | PASS | `Logs/M06A_Export.log` |

PlayMode suite записала `Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json` schema v4 только после полного прохождения.

## 8. Performance

Pure-managed timings:

- `1/2/4` substeps: `2.571855 / 3.552010 / 5.962805 µs/tick`;
- telemetry snapshot: `6.048860 µs/tick`;
- scripted validation: `6.952690 µs/tick`;
- measured allocations: `0 B/tick`.

Production telemetry после `50` warmup samples (`356` measured samples):

- simulation mean `0.038197 ms`, linear p95 `0.0461 ms`, max `0.0629 ms`;
- backend sample mean `0.020836 ms`;
- backend apply mean `0.006555 ms`.

Отдельный real-backend scripted PhysX window выполнил по `2400` measured ticks в режимах telemetry consumer off/on. Средние combined времена — `0.038737 / 0.036251 ms`, средние времена блокирующего `Physics.Simulate` — `0.024990 / 0.023025 ms`; минимум четыре контакта, `0` invalid states и `0 B/tick`.

Profiler marker `Physics.Processing` недоступен, хотя blocking `Physics.Simulate` wall-clock измерен. GPU, Player build, общий frame time и 60 FPS не измерены. Performance evidence проходит только как bounded regression sanity, а не production frame-budget proof.

## 9. Known deviations

- нет donor dynamic fixtures для честной parity calibration;
- physics proxy mass не равна сумме logical part masses;
- surface behavior provisional; dynamic coast-down ordering не подтверждён;
- только один raycast wheel backend, comparison `N/A`;
- production world route ограничен двумя pilot cells;
- continuous road/bridge route, curb/ditch, recovery и full collision policy не закрыты;
- pilot cells не приняты по donor fidelity;
- PhysX межмашинная детерминированность не заявляется;
- пользователь вручную принял bounded M06A prototype baseline 2026-07-16; это не означает donor parity или production performance acceptance.

Полный список: `Docs/VehicleValidation/KNOWN_DEVIATIONS.md`.

## 10. Созданные и обновлённые артефакты

- framework и profile under `Assets/Game/Vehicle/Simulation/Validation/` и `Assets/Game/Vehicle/Content/Validation/`;
- Editor builder, validator, exporter, performance audit и dashboard under `Assets/Game/Editor/VehicleValidation/`;
- focused EditMode/PlayMode tests under `Assets/Game/Tests/`;
- explicit config ownership и bounded production surface metadata;
- `Docs/VehicleValidation/VALIDATION_PLAN.md`;
- `Docs/VehicleValidation/CALIBRATION_TARGETS.csv`;
- `Docs/VehicleValidation/CALIBRATION_RUNS.csv`;
- `Docs/VehicleValidation/METRIC_RESULTS.csv`;
- `Docs/VehicleValidation/TUNING_CHANGE_LOG.csv`;
- `Docs/VehicleValidation/M06A_PHYSX_RUN_EVIDENCE.json`;
- `Docs/VehicleValidation/M06A_PHYSICS_VALIDATION_PERFORMANCE.json`;
- `Docs/VehicleValidation/Telemetry/` — `7` CSV;
- `Docs/VehicleValidation/WORLD_INTEGRATION_REPORT.md`;
- `Docs/VehicleValidation/PERFORMANCE_REPORT.md`;
- `Docs/VehicleValidation/KNOWN_DEVIATIONS.md`.

## 11. Ручная проверка и acceptance

Пользователь принял Milestone 06A 2026-07-16 после автоматического gate. Для будущей регрессии используется тот же маршрут:

1. Запустить isolated validation scene и оценить start/idle, ручной clutch engagement, launch, braking, steering/slalom, bump recovery и hill start.
2. Запустить bounded production fixture через Bootstrap, выехать из гаража, пересечь `z = -1024` и проверить отсутствие визуального провала/рывка/потери управления.
3. Подтвердить, что текущий handling приемлем как prototype baseline, без заявления donor parity.
4. Отдельно выполнить Player/Profiler capture, если требуется production performance acceptance; автоматический M06A gate этого не заменяет.

## 12. Go/no-go и следующий milestone

- Automated M06A gate: **PASS**.
- Текущий статус: **Accepted / HumanAccepted**.
- `GO` для перехода к bounded Milestone 06B; Milestone 07 пока не начинается.
- `NO-GO` для donor-world parity и полной production vehicle realism.
- Ровно следующий milestone: **06B_PRODUCTION_WORLD_CELL_FIDELITY_GATE**.

Milestone 06A на этом останавливается.
