# Checklist: vehicle geometry и assembly

Общий протокол — в `MANUAL_CAPTURE_GUIDE.md`. Transform не доказывает compatibility, install rule или fastener semantics.

## Preconditions

- [ ] Vehicle state named: installed parts, wheel/tire variant, fluids, electrical state, cargo and damage.
- [ ] Donor object identity/locator and coordinate space verified before measurement.
- [ ] Detached and installed states are reachable through normal gameplay without save editing.

## P0 geometry

- [ ] Overall assembled length/width/height and body-only envelope distinguished.
- [ ] Wheelbase, front/rear tracks and four wheel anchors cross-checked.
- [ ] Ride height at named chassis markers and load state.
- [ ] Fitted tire identity, rolling/static radius, width and orientation.
- [ ] Root/assembled/curb mass definitions separated; center-of-mass reference captured if discoverable.

## P0 representative assembly chain

- [x] Rear brake drum: mesh dimensions, pivot and installed mount transform cross-checked.
- [x] Candidate compatible mount identity and alignment rule (`0.01 m` collider overlap; no separate donor angular compare).
- [x] Attach prerequisites, snap/placement behavior and completion state.
- [x] Detach prerequisites and wheel-installed blocked case.
- [x] Every fastener: count, local position, tool size, direction, discrete stage count and completion gate.
- [x] Three clean install/remove repetitions with identical outcome.

## P1/P2 expansion

- [ ] 12–20 representative parts: mass, pivot, mounts, dependencies and installation order.
- [ ] Door/hood/trunk pivots, motion range, latch/blocked states.
- [ ] Fluid capacities, fill/drain points and accepted fluid relationships.
- [ ] Electrical terminals and connection relationships.
- [ ] Part damage/wear states only as observations, without simulation implementation.

## Acceptance / M05 gate

- [x] `P0-ASSEMBLY-MOUNT-RULE` is `Covered`.
- [x] `P0-ASSEMBLY-FASTENER-SEMANTICS` is `Covered`.
- [x] Representative fixture is reproducible and contains no guessed tool/turn values.
- [x] Assembly-specific gate for `05_VEHICLE_ASSEMBLY.md` is open.

## Static trace 2026-07-14

Read-only trace `04B-STATIC-DRUM-FSM-20260714` и review диагностического видео зафиксированы в `Sessions/04B_VEHICLE_ASSEMBLY_STATIC_TRACE_20260714.md`, `Sessions/04B_VEHICLE_ASSEMBLY_VIDEO_REVIEW_20260714.md` и dataset `04B.3`.

Подтверждено статически:

- rear-left candidate определяется trigger sphere radius `0.01 m` в local position `(-0.1, 0, 0)`, tag `PART` и child identity `drum brake(Clone)`;
- install требует `Trailarm_RL.Data.Installed=true` и `Trailarm_RL.Data.Bolted=true`, затем выставляет `Drumbrake_RL.Data.Installed=true`;
- removal требует active `TriggerWheelRL_New` и `Drumbrake_RL.Data.Bolted=false`;
- один `BoltPM` control marker использует discrete stage `0..8`, шаг `+1/-1`, completion gates `0` и `8`;
- tool check сравнивает `BoltPM.localScale.x=1.4` с `ToolWrenchSize`; это соответствует ключу `14`, а не unused/default `Screw.BoltSize=0`;
- `Mouse ScrollWheel > 0` отправляет `TIGHTEN`, `< 0` — `UNTIGHTEN`;
- диагностическое видео отвергает выбранный ключ `11`, но содержит `0` валидных clean trials.

На этапе dataset `04B.3` ещё не было подтверждено runtime-наблюдением:

- angular/snap acceptance и blocked-case presentation;
- смысл active `TriggerWheelRL_New` как конкретного wheel state;
- clean runtime-подтверждение ключа `14` и направления input, а также physical turn/angle/torque semantics одного stage;
- три чистых install/remove repetition.

## Runtime completion 2026-07-14

External video `My Summer Car 2026-07-14 18-52-48.mp4` (SHA-256 `86ad948bda1450fb8d2cf32583b51d0c2bc55ccef5aad9f428da3bc38e8e3c84`) и review `Sessions/04B_VEHICLE_ASSEMBLY_RUNTIME_REPETITIONS_20260714.md` подтверждают:

- явный выбор ключа `14`;
- три одинаковых install → full forward progression → full reverse progression → remove trials;
- runtime snap и восстановление removal после полного untighten;
- отсутствие колеса во всех трёх успешных removal trials.

Пользователь отдельно подтвердил observed blocked case: при установленном rear-left wheel снятие барабана блокируется. Attestation зафиксирован в `Sessions/04B_VEHICLE_ASSEMBLY_BLOCKED_REMOVAL_ATTESTATION_20260714.md` с `Medium` confidence. Exact static trace показывает, что candidate использует collider overlap и не содержит отдельного angular compare; поэтому project-authored orientation tolerance не объявляется donor measurement.

Итог dataset `04B.4`: оба assembly requirement имеют `Covered`, representative fixture — `Ready`, assembly-specific M05 gate открыт. Физический torque не заявляется: donor fastener contract дискретный `0..8`.
