# Milestone 06A — известные отклонения

## Reference coverage

1. Нет валидированных donor targets для acceleration, braking, steering, suspension, gear ratios, clutch, torque, top speed и coast-down.
2. Donor root Rigidbody `389 kg` — точное сериализованное значение kinematic-компонента, но не масса полностью собранной машины.
3. Радиус candidate mesh `tire_stock` `0.272667 m` имеет неподтверждённую fitted identity и остаётся `PlausibilityTarget` / `NeedsReview`.
4. Body AABB `1.465916 × 1.1102937 × 3.5462036 m` относится к `datsun_body`, а не ко всему собранному автомобилю.
5. Размеры garage fixture — project-authored, не завершённые donor measurements.

## Масса и центр масс

6. Масса physics proxy — `650 kg`, классификация `RemakeDesignTarget`.
7. Логические assembly definitions суммарно дают `871.7 kg`, но используются для prerequisite graph и исключены из физической калибровки.
8. CoM `(0, 0.2, 0) m` перенесён из неявного builder behavior в central config без числового изменения. Это не donor CoM.
9. Part-weighted logical CoM примерно `(-0.00494, 0.07793, 0.03791) m` не применяется, поскольку part masses не подтверждены как физические contributions.
10. Ground clearance proxy выглядит завышенным; donor ride height и ground clearance остаются `Unknown`.

## Simulation model

11. Четыре M06 substeps относятся к чистой powertrain/support логике, а не к PhysX/contact substeps.
12. Raycast backend не моделирует swept tire volume, load-sensitive tire curves, camber/caster/toe, Ackermann, anti-roll и aerodynamic drag.
13. Brake torque одинаково подаётся на четыре колеса; handbrake config пока не подключён.
14. Поддерживается один open differential и одна configurable driven pair; текущая конфигурация FWD.
15. Vehicle speed хранится как unsigned magnitude в gravity plane; reverse требует отдельной signed longitudinal metric.
16. Coast-down и top-speed trend зависят от provisional rolling resistance и Rigidbody damping, donor comparison отсутствует.
17. Surface multipliers — project design ordering, а не измеренные коэффициенты трения.
18. Typed surface lookup и ordering config coefficients проходят, но короткий dynamic coast-down не воспроизводит строгий порядок потерь: Paved `0.320246`, Gravel `0.321290`, Dirt `0.319743`, Grass `0.313770 m/s`. Эти четыре результата остаются `Informational`, а не доказательством donor surface behavior.
19. Внутренние backend policies вроде slip floor, angular-speed cap и low-speed rest threshold пока остаются кодовыми constants; они не объявляются calibrated values.

## World

20. Полная 05C geometry transfer — reference/blockout coverage, а не готовая production road/collision карта.
21. Runtime streaming содержит только две production pilot cells.
22. Пользователь 2026-07-16 отклонил их визуальное и пространственное сходство с оригиналом; пригодность ограничена технической проверкой pipeline.
23. Непрерывного production road/bridge route нет: road bindings `0/29`, bridge bindings `0/2`.
24. M06A semantic surfaces покрывают только garage/driveway/road/terrain bounded fixture.
25. Collision layer и `PhysicMaterial` policy для полного мира не закрыты.
26. Непрерывный drive доказан только от старта у гаража до пересечения `z = -1024` (`12.255066 m`). Next-cell-only поддержка на `z = -970` проверена отдельным contact probe с отключёнными colliders предыдущей ячейки; это не доказательство непрерывного проезда от границы до probe.
27. Автоматическая загрузка `cell_0_-2`, четыре контакта и vertical delta не более `0.000231 m` подтверждены только для bounded home fixture и не доказывают full-world continuity.

## Performance и repeatability

28. Editor Stopwatch audit измеряет pure simulation logic, telemetry snapshot и scripted-input overhead. Отдельный PlayMode-контур выполняет реальный backend и измеряет wall-clock блокирующего `Physics.Simulate`, но profiler marker `Physics.Processing` в batch run недоступен.
29. Production telemetry измеряет инструментированные `simulation_ms`, backend sample и backend apply, а scripted PhysX window — суммарный blocking simulation cost; ни один контур не измеряет полный streaming/frame cost, rendering или GPU.
30. Player build, development build, Profiler capture и общий 60 FPS gate не выполнены.
31. Сравнение custom/simple wheel backend имеет статус `N/A`: второго backend нет.
32. PhysX не объявляется детерминированным между машинами; используются repeated trials и tolerance-based assertions.
33. JSON performance evidence зависит от AMD Ryzen 9 5950X, Null Device и Editor batch mode; его нельзя переносить как универсальный budget.

## Scope

34. M06A не добавляет weather, UI, новые vehicle features, production art или широкую world reconstruction.
35. Числовая physics tuning не менялась: доступные reference данные недостаточны для честной donor calibration.
36. Двухсекундная idle settle phase и adaptive scripted clutch стабилизируют тестовый driver/protocol; они не являются tuning автомобиля и не доказывают качество ручного clutch feel.
37. Validation scene является development-only и не включается в production Build Settings.
38. Автоматический gate и ручное принятие bounded prototype baseline записаны 2026-07-16; текущий статус — `Accepted / HumanAccepted`.
39. Это принятие разрешает перейти только к bounded Milestone 06B; для donor-world parity, production performance acceptance и полной production vehicle realism действует `NO-GO`.
