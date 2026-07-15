# Checklist: player и interaction

Общий протокол, evidence naming и save safety — в `MANUAL_CAPTURE_GUIDE.md`. Не использовать значения Milestone 04 как donor measurement.

## Preconditions

- [ ] Build/settings/session ID зафиксированы; level route ровный и имеет проверенные start/end gates.
- [ ] Один и тот же input device, sensitivity, frame cap и FOV во всех trials.
- [ ] Состояние игрока, footwear/load/needs и тестовых объектов описано.

## P0

- [ ] Standing eye/camera height: сверить существующую static запись `932da7530b44aafe3dcf2fbe1fbfa555` runtime-кадром.
- [ ] Walk: 5 проходов в обе стороны; distance, time, acceleration/deceleration, median/range.
- [ ] Sprint: 5 проходов; steady speed, transition delay, stamina/needs precondition.
- [ ] Crouch: eye/body height, enter/exit duration, movement speed, collider clearance; 5 trials.
- [ ] Interaction/pickup reach: approaching/receding threshold, объект и ray/aim orientation; 5 trials.
- [ ] Carry/place/drop/throw: минимум 3 mass classes, held distance/orientation, release trajectory and collision outcome; 5 trials на класс.

## P1/P2

- [ ] Jump behavior, если присутствует: takeoff, apex, duration, air control.
- [ ] Practical carried mass and failure behavior.
- [ ] Seat enter/exit anchors and blocked-exit outcome.
- [ ] Tool-use reach and target alignment.
- [ ] Camera FOV, head bob/view motion at idle/walk/sprint/crouch.

## Acceptance

- [ ] Every value has unit, coordinate space, source/evidence, raw observation, tolerance and confidence.
- [ ] Invalid trials remain in log with reason.
- [ ] P0 fixture remains `Partial` until sprint, crouch, reach and pickup/throw requirements close.
